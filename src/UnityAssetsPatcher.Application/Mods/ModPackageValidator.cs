using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

internal sealed record ModPackageIndex(
    IModArchiveEntry ManifestEntry,
    IReadOnlyDictionary<string, IModArchiveEntry> FileEntries);

internal static class ModPackageValidator
{
    private const long MaxPackageSize = 10L * 1024L * 1024L * 1024L;

    public static OperationResult<ModPackageIndex> Validate(
        IModArchiveSession package,
        string packagePath,
        CancellationToken cancellationToken)
    {
        var state = new ValidationState();

        foreach (IModArchiveEntry entry in package.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var failure = ValidateEntry(entry, packagePath, state);

            if (failure is not null)
            {
                return failure;
            }
        }

        return CreateIndexResult(state, packagePath);
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

    private static OperationFailed<ModPackageIndex>? ValidateEntry(
        IModArchiveEntry entry,
        string packagePath,
        ValidationState state)
    {
        bool isDirectory = string.IsNullOrEmpty(entry.Name);

        if (!TryNormalizePath(entry.FullName, isDirectory, out string normalizedPath))
        {
            return Failure(ModPackageErrorCodes.UnsafeEntryPath, packagePath, ("entry_path", entry.FullName));
        }

        if (!state.AllEntries.Add(normalizedPath))
        {
            return Failure(ModPackageErrorCodes.DuplicateEntry, packagePath, ("entry_path", normalizedPath));
        }

        if (isDirectory)
        {
            return null;
        }

        if (entry.Length > MaxPackageSize - state.PackageSize)
        {
            return Failure(
                ModPackageErrorCodes.PackageTooLarge,
                packagePath,
                ("maximum_bytes", MaxPackageSize));
        }

        state.PackageSize += entry.Length;
        state.FileEntries.Add(normalizedPath, entry);

        if (string.Equals(GetFileName(normalizedPath), "manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            state.Manifests.Add(entry);
        }

        return null;
    }

    private static OperationResult<ModPackageIndex> CreateIndexResult(ValidationState state, string packagePath)
    {
        return state.Manifests.Count switch
        {
            0 => Failure(ModPackageErrorCodes.MissingManifest, packagePath),
            > 1 => Failure(ModPackageErrorCodes.MultipleManifests, packagePath),
            _ => new OperationSucceeded<ModPackageIndex>(
                new ModPackageIndex(state.Manifests[0], state.FileEntries))
        };
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

    private sealed class ValidationState
    {
        public Dictionary<string, IModArchiveEntry> FileEntries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AllEntries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<IModArchiveEntry> Manifests { get; } = [];
        public long PackageSize { get; set; }
    }
}
