namespace UnityAssetsPatcher.Domain.Assets;

public static class AssetFieldPath
{
    public static TField? Find<TField>(
        TField root,
        string path,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren,
        Func<TField, string?> getValue,
        Func<TField, string, IEnumerable<TField>> getChildrenByName)
        where TField : class
    {
        var resolver = new AssetFieldPathResolver<TField>(
            root,
            getName,
            getChildren,
            getValue,
            getChildrenByName,
            cacheLookups: false);

        return resolver.Find(path);
    }
}

public sealed class AssetFieldPathResolver<TField> where TField : class
{
    private readonly TField _root;
    private readonly Func<TField, string> _getName;
    private readonly Func<TField, IEnumerable<TField>> _getChildren;
    private readonly Func<TField, string?> _getValue;
    private readonly Func<TField, string, IEnumerable<TField>> _getChildrenByName;
    private readonly bool _cacheLookups;

    private readonly Dictionary<TField, ChildIndex> _childIndexes =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<string, TField?> _firstDescendantsByName = new(StringComparer.Ordinal);

    private sealed record Segment(string Name, Selector? Selector);

    private sealed record Selector(string FieldName, string Value);

    private sealed record ChildIndex(
        IReadOnlyList<TField> Children,
        IReadOnlyDictionary<string, List<TField>> ChildrenByName);

    public AssetFieldPathResolver(
        TField root,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren,
        Func<TField, string?> getValue,
        Func<TField, string, IEnumerable<TField>> getChildrenByName,
        bool cacheLookups = true)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(getName);
        ArgumentNullException.ThrowIfNull(getChildren);
        ArgumentNullException.ThrowIfNull(getValue);
        ArgumentNullException.ThrowIfNull(getChildrenByName);

        _root = root;
        _getName = getName;
        _getChildren = getChildren;
        _getValue = getValue;
        _getChildrenByName = getChildrenByName;
        _cacheLookups = cacheLookups;
    }

    public TField? Find(string path)
    {
        IReadOnlyList<Segment> segments = Parse(path);

        if (segments is [{ Selector: null }])
        {
            if (!_cacheLookups)
            {
                return FindDescendantByName(_root, segments[0].Name);
            }

            if (_firstDescendantsByName.TryGetValue(segments[0].Name, out TField? memoized))
            {
                return memoized;
            }

            TField? descendant = FindDescendantByName(_root, segments[0].Name);
            _firstDescendantsByName.Add(segments[0].Name, descendant);

            return descendant;
        }

        TField? current = _root;

        foreach (Segment segment in segments)
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

    private static IReadOnlyList<Segment> Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Field path cannot be empty.");
        }

        return path.Split('.')
            .Select(ParseSegment)
            .ToArray();
    }

    private static Segment ParseSegment(string segment)
    {
        int selectorStart = segment.IndexOf('[', StringComparison.Ordinal);

        if (selectorStart < 0)
        {
            return string.IsNullOrWhiteSpace(segment)
                ? throw new InvalidOperationException("Field path contains an empty segment.")
                : new Segment(segment, null);
        }

        if (!segment.EndsWith(']') || selectorStart == 0)
        {
            throw new InvalidOperationException($"Field path segment has invalid selector syntax: {segment}");
        }

        string name = segment[..selectorStart];
        string selector = segment[(selectorStart + 1)..^1];
        int equalsIndex = selector.IndexOf('=', StringComparison.Ordinal);

        if (equalsIndex <= 0 || equalsIndex == selector.Length - 1)
        {
            throw new InvalidOperationException($"Field path segment has invalid selector syntax: {segment}");
        }

        return new Segment(name, new Selector(selector[..equalsIndex], selector[(equalsIndex + 1)..]));
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

    private TField? FindChild(TField field, Segment segment)
    {
        IEnumerable<TField> candidates = FindChildren(field, segment.Name);

        if (segment.Selector is null)
        {
            return candidates.FirstOrDefault();
        }

        return candidates.FirstOrDefault(child =>
            MatchesSelector(child, segment.Selector));
    }

    private bool MatchesSelector(TField field, Selector selector)
    {
        TField? selectorField = FindChildren(field, selector.FieldName).FirstOrDefault();

        return selectorField is not null &&
               string.Equals(_getValue(selectorField), selector.Value, StringComparison.Ordinal);
    }

    private IEnumerable<TField> FindChildren(TField field, string name)
    {
        if (!_cacheLookups)
        {
            return _getChildrenByName(field, name);
        }

        ChildIndex childIndex = GetChildIndex(field);

        return childIndex.ChildrenByName.TryGetValue(name, out List<TField>? children)
            ? children
            : [];
    }

    private ChildIndex GetChildIndex(TField field)
    {
        if (_childIndexes.TryGetValue(field, out ChildIndex? childIndex))
        {
            return childIndex;
        }

        var childrenByName = new Dictionary<string, List<TField>>(StringComparer.Ordinal);
        var children = new List<TField>();

        foreach (TField child in _getChildren(field))
        {
            children.Add(child);
            string name = _getName(child);

            if (!childrenByName.TryGetValue(name, out List<TField>? namedChildren))
            {
                namedChildren = [];
                childrenByName.Add(name, namedChildren);
            }

            namedChildren.Add(child);
        }

        childIndex = new ChildIndex(children, childrenByName);
        _childIndexes.Add(field, childIndex);

        return childIndex;
    }
}
