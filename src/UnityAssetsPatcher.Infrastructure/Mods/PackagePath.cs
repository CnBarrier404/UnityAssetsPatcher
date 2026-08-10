namespace UnityAssetsPatcher.Infrastructure.Mods;

internal static class PackagePath
{
    public static bool TryNormalize(string path, bool isDirectory, out string normalizedPath)
    {
        normalizedPath = path.Replace('\\', '/');

        if (isDirectory)
        {
            normalizedPath = normalizedPath.TrimEnd('/');
        }

        if (normalizedPath.Length == 0 ||
            normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(path) ||
            IsDriveQualified(normalizedPath))
        {
            return false;
        }

        string[] segments = normalizedPath.Split('/');

        return segments.All(IsSafeSegment);
    }

    public static string GetFileName(string normalizedPath)
    {
        int separatorIndex = normalizedPath.LastIndexOf('/');

        return separatorIndex < 0 ? normalizedPath : normalizedPath[(separatorIndex + 1)..];
    }

    private static bool IsDriveQualified(string path)
    {
        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }

    private static bool IsSafeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) ||
            segment is "." or ".." ||
            segment.EndsWith(" ", StringComparison.Ordinal) ||
            segment.EndsWith(".", StringComparison.Ordinal) ||
            segment.IndexOfAny(['\0', '<', '>', ':', '"', '|', '?', '*']) >= 0)
        {
            return false;
        }

        return true;
    }
}
