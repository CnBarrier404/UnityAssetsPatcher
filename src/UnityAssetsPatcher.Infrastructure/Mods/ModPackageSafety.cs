namespace UnityAssetsPatcher.Infrastructure.Mods;

internal static class ModPackageSafety
{
    private const long MaxExtractionSize = 10L * 1024L * 1024L * 1024L;
    private const long MaxManifestSize = 10L * 1024L * 1024L;

    public static string NormalizeEntryPath(string packagePath, string entryPath, bool isDirectory)
    {
        string normalizedPath = entryPath.Replace('\\', '/');

        if (isDirectory)
        {
            normalizedPath = normalizedPath.TrimEnd('/');
        }

        if (normalizedPath.Length == 0 ||
            normalizedPath.StartsWith('/') ||
            Path.IsPathRooted(entryPath) ||
            IsDriveQualified(normalizedPath) ||
            normalizedPath.Split('/').Any(segment => !IsSafeSegment(segment)))
        {
            throw new InvalidDataException(
                $"Package '{packagePath}' contains an unsafe entry path: {entryPath}");
        }

        return normalizedPath;
    }

    public static string GetFileName(string normalizedPath)
    {
        int separatorIndex = normalizedPath.LastIndexOf('/');

        return separatorIndex < 0 ? normalizedPath : normalizedPath[(separatorIndex + 1)..];
    }

    public static string ResolveExtractionPath(string extractionDirectory, string entryPath)
    {
        string destinationPath = Path.GetFullPath(Path.Combine(
            extractionDirectory,
            entryPath.Replace('/', Path.DirectorySeparatorChar)));

        string relativePath = Path.GetRelativePath(extractionDirectory, destinationPath);

        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"Package entry cannot escape its extraction directory: {entryPath}");
        }

        return destinationPath;
    }

    public static void EnsureManifestSize(string entryPath, long observedSize)
    {
        if (observedSize > MaxManifestSize)
        {
            throw new InvalidDataException(
                $"Manifest entry '{entryPath}' exceeds the {MaxManifestSize}-byte size limit.");
        }
    }

    public static long ReserveExtractionBytes(string entryPath, long bytes, long totalBytes)
    {
        if (bytes < 0 || totalBytes > MaxExtractionSize - bytes)
        {
            throw new InvalidDataException(
                $"Package extraction exceeds the {MaxExtractionSize}-byte limit at entry '{entryPath}'.");
        }

        return totalBytes + bytes;
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
