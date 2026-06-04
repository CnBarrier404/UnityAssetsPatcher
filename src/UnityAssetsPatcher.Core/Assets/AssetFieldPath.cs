namespace UnityAssetsPatcher.Core.Assets;

public static class AssetFieldPath
{
    public static IReadOnlyList<AssetFieldPathSegment> Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Field path cannot be empty.");
        }

        return path.Split('.')
            .Select(ParseSegment)
            .ToArray();
    }

    private static AssetFieldPathSegment ParseSegment(string segment)
    {
        int selectorStart = segment.IndexOf('[', StringComparison.Ordinal);

        if (selectorStart < 0)
        {
            return string.IsNullOrWhiteSpace(segment)
                ? throw new InvalidOperationException("Field path contains an empty segment.")
                : new AssetFieldPathSegment(segment, null, null);
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

        return new AssetFieldPathSegment(
            name,
            selector[..equalsIndex],
            selector[(equalsIndex + 1)..]);
    }
}

public sealed record AssetFieldPathSegment(
    string Name,
    string? SelectorFieldName,
    string? SelectorValue)
{
    public bool HasSelector => SelectorFieldName is not null && SelectorValue is not null;
}
