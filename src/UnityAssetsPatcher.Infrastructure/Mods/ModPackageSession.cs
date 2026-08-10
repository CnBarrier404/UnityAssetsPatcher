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
    private readonly Lock _budgetLock = new();
    private long _reservedExtractionBytes;

    private const int CopyBufferSize = 81920;
    private const long MaxManifestSize = 10L * 1024L * 1024L;
    private const long MaxExtractionSize = 10L * 1024L * 1024L * 1024L;

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

            PackageIndex index = Validate(archive, fullPackagePath);
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

    public byte[] ReadManifest()
    {
        if (_manifestEntry.Length > MaxManifestSize)
        {
            throw ManifestTooLarge(_manifestEntry.Length);
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
                throw ManifestTooLarge(totalBytes);
            }

            output.Write(buffer, 0, bytesRead);
        }

        return output.ToArray();
    }

    public async Task<byte[]> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        if (_manifestEntry.Length > MaxManifestSize)
        {
            throw ManifestTooLarge(_manifestEntry.Length);
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
                throw ManifestTooLarge(totalBytes);
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

        if (!PackagePath.TryNormalize(source, isDirectory: false, out string normalizedSource))
        {
            throw InvalidPackage($"The package entry path is unsafe: {source}");
        }

        if (!_fileEntries.TryGetValue(normalizedSource, out ZipArchiveEntry? entry))
        {
            throw InvalidPackage($"The package entry was not found: {normalizedSource}");
        }

        long declaredLength = entry.Length;
        ReserveExtractionBytes(normalizedSource, declaredLength);

        string fullDestinationPath = Path.GetFullPath(destinationPath);
        string destinationDirectory = Path.GetDirectoryName(fullDestinationPath) ??
                                      throw new IOException("The destination directory could not be resolved.");

        _fileSystemOperations.EnsureDirectory(destinationDirectory);

        long copiedBytes = 0;
        long reservedOverageBytes = 0;

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
                        ReserveExtractionBytes(normalizedSource, additionalBytes);

                        reservedOverageBytes += additionalBytes;
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

    private static PackageIndex Validate(ZipArchive archive, string packagePath)
    {
        var fileEntries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var allEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<ZipArchiveEntry>();

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            bool isDirectory = string.IsNullOrEmpty(entry.Name);

            if (!PackagePath.TryNormalize(entry.FullName, isDirectory, out string normalizedPath))
            {
                throw InvalidPackage(packagePath, $"The package entry path is unsafe: {entry.FullName}");
            }

            if (!allEntries.Add(normalizedPath))
            {
                throw InvalidPackage(packagePath, $"The package contains a duplicate entry: {normalizedPath}");
            }

            if (isDirectory)
            {
                continue;
            }

            fileEntries.Add(normalizedPath, entry);

            if (string.Equals(
                    PackagePath.GetFileName(normalizedPath),
                    "manifest.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                manifests.Add(entry);
            }
        }

        if (manifests.Count == 0)
        {
            throw InvalidPackage(packagePath, "The package does not contain a manifest.json file");
        }

        if (manifests.Count > 1)
        {
            throw InvalidPackage(packagePath, "The package contains multiple manifest.json files");
        }

        return new PackageIndex(manifests[0], fileEntries);
    }

    private InvalidDataException ManifestTooLarge(long observedLength)
    {
        return InvalidPackage(
            $"The package manifest exceeds the {MaxManifestSize}-byte limit: " +
            $"{_manifestEntry.FullName} ({observedLength} bytes observed)");
    }

    private void ReserveExtractionBytes(string entryPath, long bytes)
    {
        lock (_budgetLock)
        {
            if (bytes < 0 || _reservedExtractionBytes > MaxExtractionSize - bytes)
            {
                throw InvalidPackage(
                    $"The package exceeds the {MaxExtractionSize}-byte extraction limit while reading: {entryPath}");
            }

            _reservedExtractionBytes += bytes;
        }
    }

    private InvalidDataException InvalidPackage(string detail)
    {
        return InvalidPackage(_packagePath, detail);
    }

    private static InvalidDataException InvalidPackage(string packagePath, string detail)
    {
        return new InvalidDataException($"{detail}. Package: {packagePath}");
    }

    private sealed record PackageIndex(
        ZipArchiveEntry ManifestEntry,
        IReadOnlyDictionary<string, ZipArchiveEntry> FileEntries);
}
