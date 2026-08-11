using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

internal sealed class ModPackageSession : IModPackageSession
{
    private readonly ZipArchive _archive;
    private readonly ZipArchiveEntry _manifestEntry;
    private readonly IReadOnlyDictionary<string, ZipArchiveEntry> _fileEntries;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<ModPackageSession> _logger;
    private readonly string _packagePath;

    private const int CopyBufferSize = 81920;

    private ModPackageSession(
        string packagePath,
        ZipArchive archive,
        ZipArchiveEntry manifestEntry,
        IReadOnlyDictionary<string, ZipArchiveEntry> fileEntries,
        IFileSystemOperations fileSystemOperations,
        ILogger<ModPackageSession> logger)
    {
        _packagePath = packagePath;
        _archive = archive;
        _manifestEntry = manifestEntry;
        _fileEntries = fileEntries;
        _fileSystemOperations = fileSystemOperations;
        _logger = logger;
    }

    public static IModPackageSession Open(
        string packagePath,
        IFileSystemOperations fileSystemOperations,
        ILogger<ModPackageSession> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(logger);

        string fullPackagePath = Path.GetFullPath(packagePath);
        Stream? stream = fileSystemOperations.OpenRead(fullPackagePath);
        ZipArchive? archive = null;

        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            stream = null;

            ModPackageIndex index = ModPackageValidator.Validate(archive, fullPackagePath);
            var session = new ModPackageSession(
                fullPackagePath,
                archive,
                index.ManifestEntry,
                index.FileEntries,
                fileSystemOperations,
                logger);

            archive = null;

            return session;
        }
        finally
        {
            archive?.Dispose();
            stream?.Dispose();
        }
    }

    public Task<byte[]> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        return ModPackageManifest.ReadAsync(
            _manifestEntry,
            _packagePath,
            _logger,
            cancellationToken);
    }

    public long CopyEntryToNewFile(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModPackageValidator.TryNormalizePath(source, isDirectory: false, out string normalizedSource))
        {
            throw new InvalidDataException(
                $"The package entry path is unsafe: {source}. Package: {_packagePath}");
        }

        if (!_fileEntries.TryGetValue(normalizedSource, out ZipArchiveEntry? entry))
        {
            throw new InvalidDataException(
                $"The package entry was not found: {normalizedSource}. Package: {_packagePath}");
        }

        string fullDestinationPath = Path.GetFullPath(destinationPath);
        string destinationDirectory = Path.GetDirectoryName(fullDestinationPath) ??
                                      throw new IOException("The destination directory could not be resolved.");

        _fileSystemOperations.EnsureDirectory(destinationDirectory);

        long copiedBytes = 0;
        TimeSpan decompressionElapsed = TimeSpan.Zero;

        _fileSystemOperations.WriteFileAtomically(
            fullDestinationPath,
            FileDestinationMode.CreateNew,
            output =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using Stream input = entry.Open();
                byte[] buffer = new byte[CopyBufferSize];
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    int bytesRead;
                    while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        copiedBytes += bytesRead;

                        if (copiedBytes > entry.Length)
                        {
                            throw new InvalidDataException(
                                $"The package entry exceeds its declared size: {normalizedSource}. " +
                                $"Package: {_packagePath}");
                        }

                        output.Write(buffer, 0, bytesRead);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
                finally
                {
                    stopwatch.Stop();
                    decompressionElapsed = stopwatch.Elapsed;
                }
            });

        ModPackageLog.EntryExtracted(
            _logger,
            entry.FullName,
            _packagePath,
            fullDestinationPath,
            copiedBytes,
            decompressionElapsed.TotalMilliseconds);

        return copiedBytes;
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}
