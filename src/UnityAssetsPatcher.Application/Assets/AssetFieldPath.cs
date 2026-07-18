namespace UnityAssetsPatcher.Application.Assets;

public static class AssetFieldPath
{
    private sealed record Segment(string Name, Selector? Selector);

    private sealed record Selector(string FieldName, string Value);

    public static TField? Find<TField>(
        TField root,
        string path,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren,
        Func<TField, string?> getValue,
        Func<TField, string, IEnumerable<TField>> getChildrenByName)
        where TField : class
    {
        var segments = Parse(path);

        if (segments is [{ Selector: null }])
        {
            return FindDescendantByName(root, segments[0].Name, getName, getChildren);
        }

        TField? current = root;

        foreach (Segment segment in segments)
        {
            current = FindChild(current, segment, getValue, getChildrenByName);

            if (current is null)
            {
                return null;
            }
        }

        return current;
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

    private static TField? FindDescendantByName<TField>(
        TField field,
        string name,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren)
        where TField : class
    {
        if (string.Equals(getName(field), name, StringComparison.Ordinal))
        {
            return field;
        }

        return getChildren(field)
            .Select(child => FindDescendantByName(child, name, getName, getChildren))
            .OfType<TField>()
            .FirstOrDefault();
    }

    private static TField? FindChild<TField>(
        TField field,
        Segment segment,
        Func<TField, string?> getValue,
        Func<TField, string, IEnumerable<TField>> getChildrenByName)
        where TField : class
    {
        var candidates = getChildrenByName(field, segment.Name);

        if (segment.Selector is null)
        {
            return candidates.FirstOrDefault();
        }

        return candidates.FirstOrDefault(child =>
            MatchesSelector(child, segment.Selector, getValue, getChildrenByName));
    }

    private static bool MatchesSelector<TField>(
        TField field,
        Selector selector,
        Func<TField, string?> getValue,
        Func<TField, string, IEnumerable<TField>> getChildrenByName)
        where TField : class
    {
        TField? selectorField = getChildrenByName(field, selector.FieldName).FirstOrDefault();

        return selectorField is not null &&
               string.Equals(getValue(selectorField), selector.Value, StringComparison.Ordinal);
    }
}
