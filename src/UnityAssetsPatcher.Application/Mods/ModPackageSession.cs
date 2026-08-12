using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

internal sealed class ModPackageSession : IDisposable
{
    private readonly IModArchiveSession _package;
    private readonly IModArchiveEntry _manifestEntry;
    private readonly IReadOnlyDictionary<string, IModArchiveEntry> _fileEntries;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<ModPackageSession> _logger;
    private readonly string _packagePath;

    private const int CopyBufferSize = 81920;

    public ModPackageSession(
        string packagePath,
        IModArchiveSession package,
        ModPackageIndex index,
        IFileSystemOperations fileSystemOperations,
        ILogger<ModPackageSession> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(logger);

        _packagePath = packagePath;
        _package = package;
        _manifestEntry = index.ManifestEntry;
        _fileEntries = index.FileEntries;
        _fileSystemOperations = fileSystemOperations;
        _logger = logger;
    }

    public async Task<OperationResult<byte[]>> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ModPackageManifest.ReadAsync(
                _manifestEntry,
                _packagePath,
                _logger,
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return Failure<byte[]>(ModPackageErrorCodes.InvalidArchive);
        }
    }

    public bool ContainsEntry(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return ModPackageValidator.TryNormalizePath(source, isDirectory: false, out string normalizedSource) &&
               _fileEntries.ContainsKey(normalizedSource);
    }

    public async Task<OperationResult<long>> CopyEntryToNewFileAsync(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModPackageValidator.TryNormalizePath(source, isDirectory: false, out string normalizedSource))
        {
            return Failure<long>(ModPackageErrorCodes.UnsafeEntryPath, ("entry_path", source));
        }

        if (!_fileEntries.TryGetValue(normalizedSource, out IModArchiveEntry? entry))
        {
            return Failure<long>(ModPackageErrorCodes.MissingEntry, ("entry_path", normalizedSource));
        }

        string fullDestinationPath = Path.GetFullPath(destinationPath);
        string destinationDirectory = Path.GetDirectoryName(fullDestinationPath) ??
                                      throw new IOException("The destination directory could not be resolved.");

        _fileSystemOperations.EnsureDirectory(destinationDirectory);

        var copyState = new EntryCopyState();

        try
        {
            await using Stream input = await entry
                .OpenReadAsync(cancellationToken)
                .ConfigureAwait(false);

            WriteEntryToNewFile(entry, input, fullDestinationPath, copyState, cancellationToken);
        }
        catch (InvalidDataException) when (copyState.EntrySizeMismatch)
        {
            return Failure<long>(
                ModPackageErrorCodes.EntrySizeMismatch,
                ("entry_path", normalizedSource),
                ("declared_bytes", entry.Length),
                ("observed_bytes", copyState.CopiedBytes));
        }
        catch (InvalidDataException)
        {
            return Failure<long>(ModPackageErrorCodes.InvalidArchive);
        }

        ModPackageLog.EntryExtracted(
            _logger,
            entry.FullName,
            _packagePath,
            fullDestinationPath,
            copyState.CopiedBytes,
            copyState.DecompressionElapsed.TotalMilliseconds);

        return new OperationSucceeded<long>(copyState.CopiedBytes);
    }

    public void Dispose()
    {
        _package.Dispose();
    }

    private void WriteEntryToNewFile(
        IModArchiveEntry entry,
        Stream input,
        string destinationPath,
        EntryCopyState state,
        CancellationToken cancellationToken)
    {
        _fileSystemOperations.WriteFileAtomically(
            destinationPath,
            FileDestinationMode.CreateNew,
            output => CopyEntry(entry, input, output, state, cancellationToken));
    }

    private static void CopyEntry(
        IModArchiveEntry entry,
        Stream input,
        Stream output,
        EntryCopyState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] buffer = new byte[CopyBufferSize];
        var stopwatch = Stopwatch.StartNew();

        try
        {
            int bytesRead;

            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.CopiedBytes += bytesRead;

                if (state.CopiedBytes > entry.Length)
                {
                    state.EntrySizeMismatch = true;

                    throw new InvalidDataException();
                }

                output.Write(buffer, 0, bytesRead);
            }

            if (state.CopiedBytes != entry.Length)
            {
                state.EntrySizeMismatch = true;

                throw new InvalidDataException();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            stopwatch.Stop();
            state.DecompressionElapsed = stopwatch.Elapsed;
        }
    }

    private OperationFailed<T> Failure<T>(
        OperationErrorCode code,
        params (string Key, object? Value)[] additionalParameters)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_path"] = _packagePath,
        };

        foreach ((string key, object? value) in additionalParameters)
        {
            parameters.Add(key, value);
        }

        return new OperationFailed<T>(new OperationError(code, parameters));
    }

    private sealed class EntryCopyState
    {
        public long CopiedBytes { get; set; }
        public TimeSpan DecompressionElapsed { get; set; }
        public bool EntrySizeMismatch { get; set; }
    }
}
