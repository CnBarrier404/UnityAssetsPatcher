using System.IO.Compression;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

public sealed class ModPackageReader : IPackageReader
{
    private sealed record PackageIndex(
        ZipArchiveEntry ManifestEntry,
        IReadOnlyDictionary<string, ZipArchiveEntry> FileEntries);

    private readonly IFileSystemOperations _fileSystemOperations;

    private const int CopyBufferSize = 81920;

    public ModPackageReader(IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _fileSystemOperations = fileSystemOperations;
    }

    public PackageContent Read(
        string packagePath,
        string extractionDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(extractionDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPackagePath = Path.GetFullPath(packagePath);
        string fullExtractionDirectory = Path.GetFullPath(extractionDirectory);

        using Stream stream = _fileSystemOperations.OpenRead(fullPackagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        PackageIndex index = Validate(archive, fullPackagePath);
        byte[] manifest = ReadManifestEntry(index.ManifestEntry, cancellationToken);
        var entryPaths = ExtractEntries(
            index.FileEntries,
            index.ManifestEntry,
            fullExtractionDirectory,
            cancellationToken);

        return new PackageContent(manifest, entryPaths);
    }

    public async Task<byte[]> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPackagePath = Path.GetFullPath(packagePath);

        await using Stream stream = _fileSystemOperations.OpenRead(fullPackagePath);
        await using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        PackageIndex index = Validate(archive, fullPackagePath);

        return await ReadManifestEntryAsync(index.ManifestEntry, cancellationToken)
            .ConfigureAwait(false);
    }

    private Dictionary<string, string> ExtractEntries(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        ZipArchiveEntry manifestEntry,
        string extractionDirectory,
        CancellationToken cancellationToken)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;

        foreach ((string entryPath, ZipArchiveEntry entry) in entries)
        {
            if (ReferenceEquals(entry, manifestEntry))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            long totalBeforeEntry = totalBytes;
            _ = ModPackageSafety.ReserveExtractionBytes(entryPath, entry.Length, totalBeforeEntry);

            string destinationPath = ModPackageSafety.ResolveExtractionPath(extractionDirectory, entryPath);
            string destinationDirectory = Path.GetDirectoryName(destinationPath) ??
                                          throw new IOException("The extraction directory could not be resolved.");

            _fileSystemOperations.EnsureDirectory(destinationDirectory);

            long copiedBytes = 0;
            _fileSystemOperations.WriteFileAtomically(
                destinationPath,
                FileDestinationMode.CreateNew,
                output =>
                {
                    using Stream input = entry.Open();
                    byte[] buffer = new byte[CopyBufferSize];
                    int bytesRead;

                    while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        copiedBytes += bytesRead;

                        if (copiedBytes > entry.Length)
                        {
                            _ = ModPackageSafety.ReserveExtractionBytes(entryPath, copiedBytes, totalBeforeEntry);
                        }

                        output.Write(buffer, 0, bytesRead);
                    }
                });

            totalBytes = ModPackageSafety.ReserveExtractionBytes(
                entryPath,
                Math.Max(entry.Length, copiedBytes),
                totalBeforeEntry);
            paths.Add(entryPath, destinationPath);
        }

        return paths;
    }

    private static PackageIndex Validate(ZipArchive archive, string packagePath)
    {
        var fileEntries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var allEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<ZipArchiveEntry>();

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            bool isDirectory = string.IsNullOrEmpty(entry.Name);

            string normalizedPath = ModPackageSafety.NormalizeEntryPath(packagePath, entry.FullName, isDirectory);

            if (!allEntries.Add(normalizedPath))
            {
                throw new InvalidDataException(
                    $"Package '{packagePath}' contains a duplicate entry path: {normalizedPath}");
            }

            if (isDirectory)
            {
                continue;
            }

            fileEntries.Add(normalizedPath, entry);

            if (string.Equals(
                    ModPackageSafety.GetFileName(normalizedPath),
                    "manifest.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                manifests.Add(entry);
            }
        }

        return manifests.Count switch
        {
            0 => throw new InvalidDataException($"Package '{packagePath}' does not contain a manifest.json file."),
            > 1 => throw new InvalidDataException($"Package '{packagePath}' contains multiple manifest.json files."),
            _ => new PackageIndex(manifests[0], fileEntries)
        };
    }

    private static byte[] ReadManifestEntry(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        ModPackageSafety.EnsureManifestSize(entry.FullName, entry.Length);

        using Stream input = entry.Open();
        using MemoryStream output = new((int)entry.Length);
        byte[] buffer = new byte[CopyBufferSize];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalBytes += bytesRead;

            ModPackageSafety.EnsureManifestSize(entry.FullName, totalBytes);

            output.Write(buffer, 0, bytesRead);
        }

        return output.ToArray();
    }

    private static async Task<byte[]> ReadManifestEntryAsync(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        ModPackageSafety.EnsureManifestSize(entry.FullName, entry.Length);

        await using Stream input = await entry.OpenAsync(cancellationToken);
        using MemoryStream output = new((int)entry.Length);
        byte[] buffer = new byte[CopyBufferSize];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalBytes += bytesRead;

            ModPackageSafety.EnsureManifestSize(entry.FullName, totalBytes);

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }
}
