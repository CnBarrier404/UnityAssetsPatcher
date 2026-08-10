using System.IO.Compression;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

internal sealed class ModPackageSession : IModPackageSession
{
    private readonly ZipArchive _archive;
    private readonly ZipArchiveEntry _manifestEntry;
    private readonly IReadOnlyDictionary<string, ZipArchiveEntry> _fileEntries;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly string _packagePath;

    private const int CopyBufferSize = 81920;
    private const long MaxManifestSize = 10L * 1024L * 1024L;

    private ModPackageSession(
        string packagePath,
        ZipArchive archive,
        ZipArchiveEntry manifestEntry,
        IReadOnlyDictionary<string, ZipArchiveEntry> fileEntries,
        IFileSystemOperations fileSystemOperations)
    {
        _packagePath = packagePath;
        _archive = archive;
        _manifestEntry = manifestEntry;
        _fileEntries = fileEntries;
        _fileSystemOperations = fileSystemOperations;
    }

    public static IModPackageSession Open(
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

            ModPackageIndex index = ModPackageValidator.Validate(archive, fullPackagePath);
            var session = new ModPackageSession(
                fullPackagePath,
                archive,
                index.ManifestEntry,
                index.FileEntries,
                fileSystemOperations);

            archive = null;

            return session;
        }
        finally
        {
            archive?.Dispose();
            stream?.Dispose();
        }
    }

    public async Task<byte[]> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        if (_manifestEntry.Length > MaxManifestSize)
        {
            throw new InvalidDataException(
                $"The package manifest exceeds the {MaxManifestSize}-byte limit: " +
                $"{_manifestEntry.FullName} ({_manifestEntry.Length} bytes observed). Package: {_packagePath}");
        }

        await using Stream input = await _manifestEntry.OpenAsync(cancellationToken);
        using MemoryStream output = new((int)_manifestEntry.Length);
        byte[] buffer = new byte[CopyBufferSize];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalBytes += bytesRead;

            if (totalBytes > MaxManifestSize)
            {
                throw new InvalidDataException(
                    $"The package manifest exceeds the {MaxManifestSize}-byte limit: " +
                    $"{_manifestEntry.FullName} ({totalBytes} bytes observed). Package: {_packagePath}");
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
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

                    if (copiedBytes > entry.Length)
                    {
                        throw new InvalidDataException(
                            $"The package entry exceeds its declared size: {normalizedSource}. " +
                            $"Package: {_packagePath}");
                    }

                    output.Write(buffer, 0, bytesRead);
                }

                cancellationToken.ThrowIfCancellationRequested();
            });

        return copiedBytes;
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}
