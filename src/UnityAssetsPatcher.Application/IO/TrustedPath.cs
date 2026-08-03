namespace UnityAssetsPatcher.Application.IO;

public static class TrustedPath
{
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string NormalizeAbsolutePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    public static bool TryNormalizeRelativePath(string path, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || IsDriveQualified(path))
        {
            return false;
        }

        string[] segments = path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar);

        if (segments.Any(segment => !IsSafeSegment(segment)))
        {
            return false;
        }

        normalizedPath = string.Join(Path.DirectorySeparatorChar, segments);

        return true;
    }

    public static bool IsWithinRoot(string fullPath, string rootFullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFullPath);

        string normalizedPath = NormalizeAbsolutePath(fullPath);
        string normalizedRoot = NormalizeAbsolutePath(rootFullPath);

        if (PathComparer.Equals(normalizedPath, normalizedRoot))
        {
            return true;
        }

        string rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(rootPrefix, PathComparison);
    }

    public static bool PathsEqual(string left, string right)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(left);
        ArgumentException.ThrowIfNullOrWhiteSpace(right);

        return PathComparer.Equals(NormalizeAbsolutePath(left), NormalizeAbsolutePath(right));
    }

    public static string? FindDuplicatePath(IReadOnlyList<string> fullPaths)
    {
        ArgumentNullException.ThrowIfNull(fullPaths);

        var seen = new HashSet<string>(PathComparer);

        return fullPaths.Select(NormalizeAbsolutePath)
            .FirstOrDefault(normalizedPath => !seen.Add(normalizedPath));
    }

    private static bool IsDriveQualified(string path)
    {
        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }

    private static bool IsSafeSegment(string segment)
    {
        return !string.IsNullOrWhiteSpace(segment) &&
               segment is not ("." or "..") &&
               !segment.EndsWith(' ') &&
               !segment.EndsWith('.') &&
               segment.IndexOfAny(['\0', '<', '>', ':', '"', '|', '?', '*']) < 0;
    }
}
