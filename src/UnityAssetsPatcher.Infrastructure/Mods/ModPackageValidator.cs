using System.IO.Compression;

namespace UnityAssetsPatcher.Infrastructure.Mods;

internal sealed record ModPackageIndex(
    ZipArchiveEntry ManifestEntry,
    IReadOnlyDictionary<string, ZipArchiveEntry> FileEntries);

internal static class ModPackageValidator
{
    private const long MaxPackageSize = 10L * 1024L * 1024L * 1024L;

    public static ModPackageIndex Validate(ZipArchive archive, string packagePath)
    {
        var fileEntries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var allEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<ZipArchiveEntry>();
        _ = archive.Entries.Aggregate<ZipArchiveEntry, long>(0, (current, entry) =>
            ValidateEntry(entry, packagePath, current, allEntries, fileEntries, manifests));

        ZipArchiveEntry manifestEntry = manifests.Count switch
        {
            0 => throw new InvalidDataException(
                $"The package does not contain a manifest.json file. Package: {packagePath}"),
            > 1 => throw new InvalidDataException(
                $"The package contains multiple manifest.json files. Package: {packagePath}"),
            _ => manifests[0]
        };

        return new ModPackageIndex(manifestEntry, fileEntries);
    }

    public static bool TryNormalizePath(string path, bool isDirectory, out string normalizedPath)
    {
        normalizedPath = path.Replace('\\', '/');

        if (isDirectory)
        {
            normalizedPath = normalizedPath.TrimEnd('/');
        }

        if (normalizedPath.Length == 0 ||
            normalizedPath.StartsWith('/') ||
            Path.IsPathRooted(path) ||
            IsDriveQualified(normalizedPath))
        {
            return false;
        }

        string[] segments = normalizedPath.Split('/');

        return segments.All(IsSafeSegment);
    }

    private static string GetFileName(string normalizedPath)
    {
        int separatorIndex = normalizedPath.LastIndexOf('/');

        return separatorIndex < 0 ? normalizedPath : normalizedPath[(separatorIndex + 1)..];
    }

    private static long ValidateEntry(
        ZipArchiveEntry entry,
        string packagePath,
        long packageSize,
        HashSet<string> allEntries,
        Dictionary<string, ZipArchiveEntry> fileEntries,
        List<ZipArchiveEntry> manifests)
    {
        bool isDirectory = string.IsNullOrEmpty(entry.Name);

        if (!TryNormalizePath(entry.FullName, isDirectory, out string normalizedPath))
        {
            throw new InvalidDataException(
                $"The package entry path is unsafe: {entry.FullName}. Package: {packagePath}");
        }

        if (!allEntries.Add(normalizedPath))
        {
            throw new InvalidDataException(
                $"The package contains a duplicate entry: {normalizedPath}. Package: {packagePath}");
        }

        return isDirectory
            ? packageSize
            : IndexFileEntry(entry, normalizedPath, packagePath, packageSize, fileEntries, manifests);
    }

    private static long IndexFileEntry(
        ZipArchiveEntry entry,
        string normalizedPath,
        string packagePath,
        long packageSize,
        Dictionary<string, ZipArchiveEntry> fileEntries,
        List<ZipArchiveEntry> manifests)
    {
        if (entry.Length > MaxPackageSize - packageSize)
        {
            throw new InvalidDataException(
                $"The package exceeds the {MaxPackageSize}-byte uncompressed size limit. Package: {packagePath}");
        }

        long updatedPackageSize = packageSize + entry.Length;

        fileEntries.Add(normalizedPath, entry);

        AddManifestEntry(entry, normalizedPath, manifests);

        return updatedPackageSize;
    }

    private static void AddManifestEntry(
        ZipArchiveEntry entry,
        string normalizedPath,
        List<ZipArchiveEntry> manifests)
    {
        if (string.Equals(GetFileName(normalizedPath), "manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            manifests.Add(entry);
        }
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
