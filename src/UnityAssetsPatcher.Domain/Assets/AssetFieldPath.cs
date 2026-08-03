namespace UnityAssetsPatcher.Domain.Assets;

public sealed class AssetFieldPath : IEquatable<AssetFieldPath>
{
    public string Value { get; }
    public IReadOnlyList<AssetFieldPathSegment> Segments => _segments;

    private readonly AssetFieldPathSegment[] _segments;

    public AssetFieldPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Field path cannot be empty.", nameof(value));
        }

        _segments = value.Split('.').Select(ParseSegment).ToArray();
        Value = string.Join('.', _segments.Select(segment => segment.ToString()));
    }

    public static TField? Find<TField>(
        TField root,
        string path,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren,
        Func<TField, string?> getValue,
        Func<TField, string, IEnumerable<TField>> getChildrenByName)
        where TField : class
    {
        ArgumentNullException.ThrowIfNull(getChildrenByName);

        var resolver = new AssetFieldPathResolver<TField>(root, getName, getChildren, getValue);

        return resolver.Find(new AssetFieldPath(path));
    }

    public override string ToString()
    {
        return Value;
    }

    public bool Equals(AssetFieldPath? other)
    {
        return other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is AssetFieldPath other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Value);
    }

    private static AssetFieldPathSegment ParseSegment(string segment)
    {
        int selectorStart = segment.IndexOf('[', StringComparison.Ordinal);

        if (selectorStart < 0)
        {
            return string.IsNullOrWhiteSpace(segment)
                ? throw new ArgumentException("Field path contains an empty segment.", nameof(segment))
                : new AssetFieldPathSegment(segment, null);
        }

        if (!segment.EndsWith(']') || selectorStart == 0)
        {
            throw new ArgumentException($"Field path segment has invalid selector syntax: {segment}", nameof(segment));
        }

        string name = segment[..selectorStart];
        string selector = segment[(selectorStart + 1)..^1];
        int equalsIndex = selector.IndexOf('=', StringComparison.Ordinal);

        if (equalsIndex <= 0 || equalsIndex == selector.Length - 1)
        {
            throw new ArgumentException($"Field path segment has invalid selector syntax: {segment}", nameof(segment));
        }

        return new AssetFieldPathSegment(
            name,
            new AssetFieldSelector(selector[..equalsIndex], selector[(equalsIndex + 1)..]));
    }
}

public sealed record AssetFieldPathSegment(string Name, AssetFieldSelector? Selector)
{
    public override string ToString()
    {
        return Selector is null ? Name : $"{Name}[{Selector.FieldName}={Selector.Value}]";
    }
}

public sealed record AssetFieldSelector(string FieldName, string Value);

public sealed class AssetFieldPathResolver<TField> where TField : class
{
    private readonly TField _root;
    private readonly Func<TField, string> _getName;
    private readonly Func<TField, IEnumerable<TField>> _getChildren;
    private readonly Func<TField, string?> _getValue;
    private readonly Dictionary<TField, ChildIndex> _childIndexes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, TField?> _firstDescendantsByName = new(StringComparer.Ordinal);

    private sealed record ChildIndex(IReadOnlyDictionary<string, List<TField>> ChildrenByName);

    public AssetFieldPathResolver(
        TField root,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren,
        Func<TField, string?> getValue)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(getName);
        ArgumentNullException.ThrowIfNull(getChildren);
        ArgumentNullException.ThrowIfNull(getValue);

        _root = root;
        _getName = getName;
        _getChildren = getChildren;
        _getValue = getValue;
    }

    public TField? Find(AssetFieldPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Segments is [{ Selector: null }])
        {
            string name = path.Segments[0].Name;

            if (_firstDescendantsByName.TryGetValue(name, out TField? memoized))
            {
                return memoized;
            }

            TField? descendant = FindDescendantByName(_root, name);
            _firstDescendantsByName.Add(name, descendant);

            return descendant;
        }

        TField? current = _root;

        foreach (AssetFieldPathSegment segment in path.Segments)
        {
            current = FindChild(current, segment);

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    public void InvalidateStructure()
    {
        _childIndexes.Clear();
        _firstDescendantsByName.Clear();
    }

    private TField? FindDescendantByName(TField field, string name)
    {
        if (string.Equals(_getName(field), name, StringComparison.Ordinal))
        {
            return field;
        }

        foreach (TField child in _getChildren(field))
        {
            TField? match = FindDescendantByName(child, name);

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private TField? FindChild(TField field, AssetFieldPathSegment segment)
    {
        var candidates = FindChildren(field, segment.Name);

        return segment.Selector is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(child => MatchesSelector(child, segment.Selector));
    }

    private bool MatchesSelector(TField field, AssetFieldSelector selector)
    {
        TField? selectorField = FindChildren(field, selector.FieldName).FirstOrDefault();

        return selectorField is not null &&
               string.Equals(_getValue(selectorField), selector.Value, StringComparison.Ordinal);
    }

    private List<TField> FindChildren(TField field, string name)
    {
        ChildIndex childIndex = GetChildIndex(field);

        return childIndex.ChildrenByName.TryGetValue(name, out var children) ? children : [];
    }

    private ChildIndex GetChildIndex(TField field)
    {
        if (_childIndexes.TryGetValue(field, out ChildIndex? childIndex))
        {
            return childIndex;
        }

        var childrenByName = new Dictionary<string, List<TField>>(StringComparer.Ordinal);

        foreach (TField child in _getChildren(field))
        {
            string name = _getName(child);

            if (!childrenByName.TryGetValue(name, out var namedChildren))
            {
                namedChildren = [];
                childrenByName.Add(name, namedChildren);
            }

            namedChildren.Add(child);
        }

        childIndex = new ChildIndex(childrenByName);
        _childIndexes.Add(field, childIndex);

        return childIndex;
    }
}
