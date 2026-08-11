using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

internal sealed record ModPackageIndex(
    IModPackageEntry ManifestEntry,
    IReadOnlyDictionary<string, IModPackageEntry> FileEntries);

internal static class ModPackageValidator
{
    public const long MaxPackageSize = 10L * 1024L * 1024L * 1024L;

    public static OperationResult<ModPackageIndex> Validate(
        IModPackageSession package,
        string packagePath,
        CancellationToken cancellationToken)
    {
        var fileEntries = new Dictionary<string, IModPackageEntry>(StringComparer.OrdinalIgnoreCase);
        var allEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<IModPackageEntry>();
        long packageSize = 0;

        foreach (IModPackageEntry entry in package.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool isDirectory = string.IsNullOrEmpty(entry.Name);

            if (!TryNormalizePath(entry.FullName, isDirectory, out string normalizedPath))
            {
                return Failure(ModPackageErrorCodes.UnsafeEntryPath, packagePath, ("entry_path", entry.FullName));
            }

            if (!allEntries.Add(normalizedPath))
            {
                return Failure(ModPackageErrorCodes.DuplicateEntry, packagePath, ("entry_path", normalizedPath));
            }

            if (isDirectory)
            {
                continue;
            }

            if (entry.Length > MaxPackageSize - packageSize)
            {
                return Failure(
                    ModPackageErrorCodes.PackageTooLarge,
                    packagePath,
                    ("maximum_bytes", MaxPackageSize));
            }

            packageSize += entry.Length;
            fileEntries.Add(normalizedPath, entry);

            if (string.Equals(GetFileName(normalizedPath), "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                manifests.Add(entry);
            }
        }

        if (manifests.Count == 0)
        {
            return Failure(ModPackageErrorCodes.MissingManifest, packagePath);
        }

        if (manifests.Count > 1)
        {
            return Failure(ModPackageErrorCodes.MultipleManifests, packagePath);
        }

        return new OperationSucceeded<ModPackageIndex>(new ModPackageIndex(manifests[0], fileEntries));
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

    private static OperationFailed<ModPackageIndex> Failure(
        OperationErrorCode code,
        string packagePath,
        params (string Key, object? Value)[] additionalParameters)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_path"] = packagePath,
        };

        foreach ((string key, object? value) in additionalParameters)
        {
            parameters.Add(key, value);
        }

        return new OperationFailed<ModPackageIndex>(new OperationError(code, parameters));
    }

    private static string GetFileName(string normalizedPath)
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
        return !string.IsNullOrWhiteSpace(segment) &&
               segment is not ("." or "..") &&
               !segment.EndsWith(' ') &&
               !segment.EndsWith('.') &&
               segment.IndexOfAny(['\0', '<', '>', ':', '"', '|', '?', '*']) < 0;
    }
}
