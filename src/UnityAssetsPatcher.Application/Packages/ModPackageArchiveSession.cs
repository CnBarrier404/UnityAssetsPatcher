using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Packages;

public sealed class ModPackageArchiveSession : IDisposable
{
    private readonly IModPackageArchive _archive;
    private readonly PackageEntryInfo _manifestEntry;
    private readonly IReadOnlyDictionary<string, PackageEntryInfo> _fileEntries;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly Lock _budgetLock = new();
    private long _reservedExtractionBytes;

    private const int CopyBufferSize = 81920;
    private const long MaxManifestSize = 10L * 1024L * 1024L;
    private const long MaxExtractionSize = 10L * 1024L * 1024L * 1024L;

    internal ModPackageArchiveSession(
        IModPackageArchive archive,
        PackageEntryInfo manifestEntry,
        IReadOnlyDictionary<string, PackageEntryInfo> fileEntries,
        IFileSystemOperations fileSystemOperations)
    {
        _archive = archive;
        _manifestEntry = manifestEntry;
        _fileEntries = fileEntries;
        _fileSystemOperations = fileSystemOperations;
    }

    public OperationResult<byte[]> ReadManifest()
    {
        if (_manifestEntry.Length > MaxManifestSize)
        {
            return ManifestTooLarge(_manifestEntry.Length);
        }

        using Stream input = _archive.OpenEntry(_manifestEntry.Id);
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

        using Stream input = _archive.OpenEntry(_manifestEntry.Id);
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

        if (!_fileEntries.TryGetValue(normalizedSource, out PackageEntryInfo? entry))
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

        try
        {
            _fileSystemOperations.WriteFileAtomically(
                fullDestinationPath,
                FileDestinationMode.CreateNew,
                output =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using Stream input = _archive.OpenEntry(entry.Id);
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
                                throw new PackageLimitException(overageError);
                            }

                            reservedOverageBytes += additionalBytes;
                        }

                        output.Write(buffer, 0, bytesRead);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                });
        }
        catch (PackageLimitException exception)
        {
            return new OperationFailed<long>(exception.Error);
        }

        return new OperationSucceeded<long>(copiedBytes);
    }

    public void Dispose()
    {
        _archive.Dispose();
    }

    private OperationFailed<byte[]> ManifestTooLarge(long observedLength)
    {
        return Failure<byte[]>(
            ModPackageErrorCodes.ManifestTooLarge,
            ("entry_path", _manifestEntry.Path),
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
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_path"] = _archive.PackagePath,
        };

        foreach ((string key, object? value) in parameters)
        {
            values.Add(key, value);
        }

        return new OperationError(code, values);
    }

    private sealed class PackageLimitException : Exception
    {
        public OperationError Error { get; }

        public PackageLimitException(OperationError error)
        {
            Error = error;
        }
    }
}
