using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Packages;

public sealed class ModPackageArchiveService
{
    private readonly IModPackageArchiveFactory _archiveFactory;
    private readonly IFileSystemOperations _fileSystemOperations;

    public ModPackageArchiveService(
        IModPackageArchiveFactory archiveFactory,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(archiveFactory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _archiveFactory = archiveFactory;
        _fileSystemOperations = fileSystemOperations;
    }

    public OperationResult<ModPackageArchiveSession> OpenRead(string packagePath)
    {
        IModPackageArchive archive = _archiveFactory.OpenRead(packagePath);

        try
        {
            OperationResult<ModPackageArchiveIndex> validationResult = Validate(archive);

            if (validationResult is OperationFailed<ModPackageArchiveIndex> failure)
            {
                archive.Dispose();

                return new OperationFailed<ModPackageArchiveSession>(failure.Error);
            }

            ModPackageArchiveIndex index = ((OperationSucceeded<ModPackageArchiveIndex>)validationResult).Value;
            var session = new ModPackageArchiveSession(
                archive,
                index.ManifestEntry,
                index.FileEntries,
                _fileSystemOperations);

            return new OperationSucceeded<ModPackageArchiveSession>(session);
        }
        catch
        {
            archive.Dispose();

            throw;
        }
    }

    private static OperationResult<ModPackageArchiveIndex> Validate(IModPackageArchive archive)
    {
        var fileEntries = new Dictionary<string, PackageEntryInfo>(StringComparer.OrdinalIgnoreCase);
        var allEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<PackageEntryInfo>();

        foreach (PackageEntryInfo entry in archive.Entries)
        {
            if (!ModPackagePath.TryNormalize(entry.Path, entry.IsDirectory, out string normalizedPath))
            {
                return Failure(
                    ModPackageErrorCodes.UnsafeEntryPath,
                    archive.PackagePath,
                    ("entry_path", entry.Path));
            }

            if (!allEntries.Add(normalizedPath))
            {
                return Failure(
                    ModPackageErrorCodes.DuplicateEntry,
                    archive.PackagePath,
                    ("entry_path", normalizedPath));
            }

            if (entry.IsDirectory)
            {
                continue;
            }

            fileEntries.Add(normalizedPath, entry);

            if (string.Equals(
                    ModPackagePath.GetFileName(normalizedPath),
                    "manifest.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                manifests.Add(entry);
            }
        }

        if (manifests.Count == 0)
        {
            return Failure(ModPackageErrorCodes.ManifestMissing, archive.PackagePath);
        }

        if (manifests.Count > 1)
        {
            return Failure(ModPackageErrorCodes.MultipleManifests, archive.PackagePath);
        }

        var index = new ModPackageArchiveIndex(manifests[0], fileEntries);

        return new OperationSucceeded<ModPackageArchiveIndex>(index);
    }

    private static OperationFailed<ModPackageArchiveIndex> Failure(
        OperationErrorCode code,
        string packagePath,
        params (string Key, object? Value)[] parameters)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_path"] = packagePath,
        };

        foreach ((string key, object? value) in parameters)
        {
            values.Add(key, value);
        }

        return new OperationFailed<ModPackageArchiveIndex>(new OperationError(code, values));
    }

    private sealed record ModPackageArchiveIndex(
        PackageEntryInfo ManifestEntry,
        IReadOnlyDictionary<string, PackageEntryInfo> FileEntries);
}
