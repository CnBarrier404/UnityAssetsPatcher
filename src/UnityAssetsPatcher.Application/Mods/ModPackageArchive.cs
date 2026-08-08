using System.IO.Compression;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

internal sealed class ModPackageArchive : IDisposable
{
    public string PackagePath { get; }

    private readonly ZipArchive _archive;
    private readonly ZipArchiveEntry _manifestEntry;
    private readonly IReadOnlyDictionary<string, ZipArchiveEntry> _fileEntries;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly Lock _budgetLock = new();
    private long _reservedExtractionBytes;

    private const int CopyBufferSize = 81920;
    private const long MaxManifestSize = 10L * 1024L * 1024L;
    private const long MaxExtractionSize = 10L * 1024L * 1024L * 1024L;

    private ModPackageArchive(
        string packagePath,
        ZipArchive archive,
        ZipArchiveEntry manifestEntry,
        IReadOnlyDictionary<string, ZipArchiveEntry> fileEntries,
        IFileSystemOperations fileSystemOperations)
    {
        PackagePath = packagePath;
        _archive = archive;
        _manifestEntry = manifestEntry;
        _fileEntries = fileEntries;
        _fileSystemOperations = fileSystemOperations;
    }

    public static OperationResult<ModPackageArchive> OpenRead(
        string packagePath,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        string fullPackagePath = Path.GetFullPath(packagePath);
        Stream? stream = fileSystemOperations.OpenRead(fullPackagePath);
        ZipArchive? archive = null;

        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            stream = null;

            OperationResult<ModPackageArchiveIndex> validationResult = Validate(archive, fullPackagePath);

            if (validationResult is OperationFailed<ModPackageArchiveIndex> failure)
            {
                archive.Dispose();
                archive = null;

                return new OperationFailed<ModPackageArchive>(failure.Error);
            }

            ModPackageArchiveIndex index = ((OperationSucceeded<ModPackageArchiveIndex>)validationResult).Value;
            var packageArchive = new ModPackageArchive(
                fullPackagePath,
                archive,
                index.ManifestEntry,
                index.FileEntries,
                fileSystemOperations);

            archive = null;

            return new OperationSucceeded<ModPackageArchive>(packageArchive);
        }
        finally
        {
            archive?.Dispose();
            stream?.Dispose();
        }
    }

    public OperationResult<byte[]> ReadManifest()
    {
        if (_manifestEntry.Length > MaxManifestSize)
        {
            return ManifestTooLarge(_manifestEntry.Length);
        }

        using Stream input = _manifestEntry.Open();
        using MemoryStream output = new((int)_manifestEntry.Length);
        byte[] buffer = new byte[CopyBufferSize];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytes += bytesRead;

            if (totalBytes > MaxManifestSize)
            {
                return ManifestTooLarge(totalBytes);
            }

            output.Write(buffer, 0, bytesRead);
        }

        return new OperationSucceeded<byte[]>(output.ToArray());
    }

    public async Task<OperationResult<byte[]>> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        if (_manifestEntry.Length > MaxManifestSize)
        {
            return ManifestTooLarge(_manifestEntry.Length);
        }

        using Stream input = _manifestEntry.Open();
        using MemoryStream output = new((int)_manifestEntry.Length);
        byte[] buffer = new byte[CopyBufferSize];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalBytes += bytesRead;

            if (totalBytes > MaxManifestSize)
            {
                return ManifestTooLarge(totalBytes);
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }

        return new OperationSucceeded<byte[]>(output.ToArray());
    }

    public OperationResult<long> CopyEntryToNewFile(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModPackagePath.TryNormalize(source, isDirectory: false, out string normalizedSource))
        {
            return Failure<long>(ModPackageErrorCodes.UnsafeEntryPath, ("entry_path", source));
        }

        if (!_fileEntries.TryGetValue(normalizedSource, out ZipArchiveEntry? entry))
        {
            return Failure<long>(ModPackageErrorCodes.EntryNotFound, ("entry_path", normalizedSource));
        }

        long declaredLength = entry.Length;
        OperationError? limitError = ReserveExtractionBytes(normalizedSource, declaredLength);

        if (limitError is not null)
        {
            return new OperationFailed<long>(limitError);
        }

        string fullDestinationPath = Path.GetFullPath(destinationPath);
        string destinationDirectory = Path.GetDirectoryName(fullDestinationPath) ??
                                      throw new IOException("The destination directory could not be resolved.");

        _fileSystemOperations.EnsureDirectory(destinationDirectory);

        long copiedBytes = 0;
        long reservedOverageBytes = 0;
        OperationError? copyError = null;

        try
        {
            _fileSystemOperations.WriteFileAtomically(
                fullDestinationPath,
                FileDestinationMode.CreateNew,
                output =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using Stream input = entry.Open();
                    byte[] buffer = new byte[CopyBufferSize];
                    int bytesRead;

                    while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        copiedBytes += bytesRead;
                        long overageBytes = copiedBytes - declaredLength;

                        if (overageBytes > reservedOverageBytes)
                        {
                            long additionalBytes = overageBytes - reservedOverageBytes;
                            OperationError? overageError = ReserveExtractionBytes(normalizedSource, additionalBytes);

                            if (overageError is not null)
                            {
                                copyError = overageError;

                                throw new InvalidDataException("The package extraction limit was exceeded.");
                            }

                            reservedOverageBytes += additionalBytes;
                        }

                        output.Write(buffer, 0, bytesRead);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                });
        }
        catch (InvalidDataException) when (copyError is not null)
        {
            return new OperationFailed<long>(copyError);
        }

        return new OperationSucceeded<long>(copiedBytes);
    }

    public void Dispose()
    {
        _archive.Dispose();
    }

    private static OperationResult<ModPackageArchiveIndex> Validate(ZipArchive archive, string packagePath)
    {
        var fileEntries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var allEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<ZipArchiveEntry>();

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            bool isDirectory = string.IsNullOrEmpty(entry.Name);

            if (!ModPackagePath.TryNormalize(entry.FullName, isDirectory, out string normalizedPath))
            {
                return Failure<ModPackageArchiveIndex>(
                    ModPackageErrorCodes.UnsafeEntryPath,
                    packagePath,
                    ("entry_path", entry.FullName));
            }

            if (!allEntries.Add(normalizedPath))
            {
                return Failure<ModPackageArchiveIndex>(
                    ModPackageErrorCodes.DuplicateEntry,
                    packagePath,
                    ("entry_path", normalizedPath));
            }

            if (isDirectory)
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
            return Failure<ModPackageArchiveIndex>(ModPackageErrorCodes.ManifestMissing, packagePath);
        }

        if (manifests.Count > 1)
        {
            return Failure<ModPackageArchiveIndex>(ModPackageErrorCodes.MultipleManifests, packagePath);
        }

        var index = new ModPackageArchiveIndex(manifests[0], fileEntries);

        return new OperationSucceeded<ModPackageArchiveIndex>(index);
    }

    private OperationFailed<byte[]> ManifestTooLarge(long observedLength)
    {
        return Failure<byte[]>(
            ModPackageErrorCodes.ManifestTooLarge,
            ("entry_path", _manifestEntry.FullName),
            ("observed_bytes", observedLength),
            ("limit_bytes", MaxManifestSize));
    }

    private OperationError? ReserveExtractionBytes(string entryPath, long bytes)
    {
        lock (_budgetLock)
        {
            if (bytes < 0 || _reservedExtractionBytes > MaxExtractionSize - bytes)
            {
                return Error(
                    ModPackageErrorCodes.ExtractionLimitExceeded,
                    ("entry_path", entryPath),
                    ("limit_bytes", MaxExtractionSize));
            }

            _reservedExtractionBytes += bytes;
        }

        return null;
    }

    private OperationFailed<T> Failure<T>(
        OperationErrorCode code,
        params (string Key, object? Value)[] parameters)
    {
        return new OperationFailed<T>(Error(code, parameters));
    }

    private OperationError Error(
        OperationErrorCode code,
        params (string Key, object? Value)[] parameters)
    {
        return CreateError(code, PackagePath, parameters);
    }

    private static OperationFailed<T> Failure<T>(
        OperationErrorCode code,
        string packagePath,
        params (string Key, object? Value)[] parameters)
    {
        return new OperationFailed<T>(CreateError(code, packagePath, parameters));
    }

    private static OperationError CreateError(
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

        return new OperationError(code, values);
    }

    private sealed record ModPackageArchiveIndex(
        ZipArchiveEntry ManifestEntry,
        IReadOnlyDictionary<string, ZipArchiveEntry> FileEntries);
}
