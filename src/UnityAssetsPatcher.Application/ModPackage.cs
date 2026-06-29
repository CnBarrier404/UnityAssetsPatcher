using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application;

public sealed class ModPackage : IDisposable
{
    public IReadOnlyList<ManifestOptionalGroup> AvailableOptional { get; }
    public IReadOnlyDictionary<string, string> SourceAssetsPaths { get; }
    public ModManifest Manifest { get; }
    public string PackagePath { get; }

    private readonly Func<string, ZipArchive> _openPackageArchive;
    private readonly string? _temporaryDirectory;
    private long _reservedExtractionBytes;

    private const long MaxTotalModPackageExtractionSize = 10L * 1024L * 1024L * 1024L; // 10GB
    private const int CopyBufferSize = 81920;

    private ModPackage(
        string packagePath,
        ModManifest manifest,
        IReadOnlyList<ManifestOptionalGroup> availableOptional,
        IReadOnlyDictionary<string, string> sourceAssetsPaths,
        Func<string, ZipArchive> openPackageArchive,
        string? temporaryDirectory,
        long reservedExtractionBytes)
    {
        PackagePath = packagePath;
        Manifest = manifest;
        AvailableOptional = availableOptional;
        SourceAssetsPaths = sourceAssetsPaths;
        _openPackageArchive = openPackageArchive;
        _temporaryDirectory = temporaryDirectory;
        _reservedExtractionBytes = reservedExtractionBytes;
    }

    public static ModPackage Load(
        string modPackagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        ModManifestReader manifestReader,
        Func<string, ZipArchive> openPackageArchive,
        StepTimer timings)
    {
        string modPackageFullPath = Path.GetFullPath(modPackagePath);
        long reservedExtractionBytes = 0;

        ZipArchive? archive = null;

        if (!File.Exists(modPackageFullPath))
        {
            throw new FileNotFoundException($"Mod not found: {modPackageFullPath}");
        }

        try
        {
            ModManifest manifest = timings.Measure("read-package", () =>
            {
                archive = openPackageArchive(modPackageFullPath);

                JsonElement manifestElement = ModManifestJsonReader.ReadFromZipArchive(archive, modPackageFullPath);

                return manifestReader.Load(manifestElement);
            });

            ModManifest effectiveManifest = manifest.SelectOptional(selectedOptionalGroups);

            if (archive is null)
            {
                throw new InvalidOperationException("Mod package was not opened while reading the manifest.");
            }

            (var sourceAssetsPaths, string? temporaryDirectory) =
                timings.Measure("prepare-sources", () =>
                    ExtractSourceAssets(modPackageFullPath, effectiveManifest, archive, ref reservedExtractionBytes));

            return new ModPackage(
                modPackageFullPath,
                effectiveManifest,
                manifest.Optional,
                sourceAssetsPaths,
                openPackageArchive,
                temporaryDirectory,
                reservedExtractionBytes);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    public void CopyEntryToFile(string source, string destinationPath)
    {
        using ZipArchive archive = _openPackageArchive(PackagePath);
        ZipArchiveEntry entry = FindFileEntry(archive, source.Replace('\\', '/'), PackagePath);
        CopyEntryToNewFile(entry, destinationPath, ref _reservedExtractionBytes);
    }

    public void Dispose()
    {
        if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static (IReadOnlyDictionary<string, string> Paths, string? TemporaryDirectory) ExtractSourceAssets(
        string packagePath,
        ModManifest manifest,
        ZipArchive archive,
        ref long reservedExtractionBytes)
    {
        string[] replacementSources = manifest.Patches
            .Select(patch => patch.ReplaceFrom?.AssetsFilePath)
            .OfType<string>()
            .Select(source => source.Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (replacementSources.Length == 0)
        {
            return (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null);
        }

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher.{Guid.NewGuid():N}");

        try
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string source in replacementSources)
            {
                ZipArchiveEntry entry = FindFileEntry(archive, source, packagePath);
                string destinationPath = ResolveUnderDirectory(temporaryDirectory, source);

                CopyEntryToNewFile(entry, destinationPath, ref reservedExtractionBytes);
                paths[source] = destinationPath;
            }

            return (paths, temporaryDirectory);
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }

            throw;
        }
    }

    private static ZipArchiveEntry FindFileEntry(ZipArchive archive, string source, string packagePath)
    {
        var matches = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) &&
                            string.Equals(entry.FullName, source, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new FileNotFoundException(
                $"Zip payload file not found: {source} in {packagePath}",
                source),
            _ => throw new InvalidOperationException($"Zip payload file matched multiple entries: {source}")
        };
    }

    private static string ResolveUnderDirectory(string rootDirectory, string relativePath)
    {
        string fullRootDirectory = Path.GetFullPath(rootDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(
            fullRootDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootWithSeparator = fullRootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? fullRootDirectory
            : fullRootDirectory + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Zip payload source cannot escape its extraction directory: {relativePath}");
        }

        return fullPath;
    }

    private static void CopyEntryToNewFile(
        ZipArchiveEntry entry,
        string destinationPath,
        ref long reservedExtractionBytes)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);

        long declaredBytes = entry.Length;
        long reservedOverageBytes = 0;
        ReserveExtractionBytes(entry, declaredBytes, ref reservedExtractionBytes);

        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        string tempPath = Path.Combine(
            string.IsNullOrEmpty(destinationDirectory) ? Directory.GetCurrentDirectory() : destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (Stream input = entry.Open())
            using (FileStream output = File.Create(tempPath))
            {
                byte[] buffer = new byte[CopyBufferSize];
                long copiedBytes = 0;
                int bytesRead;

                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    copiedBytes += bytesRead;
                    ReserveOverageIfNeeded(
                        entry,
                        declaredBytes,
                        copiedBytes,
                        ref reservedOverageBytes,
                        ref reservedExtractionBytes);
                    output.Write(buffer, 0, bytesRead);
                }
            }

            try
            {
                File.Move(tempPath, destinationPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                throw new IOException(
                    $"Payload file was created by another process during installation: {destinationPath}");
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void ReserveOverageIfNeeded(
        ZipArchiveEntry entry,
        long declaredBytes,
        long copiedBytes,
        ref long reservedOverageBytes,
        ref long reservedExtractionBytes)
    {
        long overageBytes = copiedBytes - declaredBytes;

        if (overageBytes <= reservedOverageBytes)
        {
            return;
        }

        long additionalBytes = overageBytes - reservedOverageBytes;
        ReserveExtractionBytes(entry, additionalBytes, ref reservedExtractionBytes);
        reservedOverageBytes += additionalBytes;
    }

    private static void ReserveExtractionBytes(
        ZipArchiveEntry entry,
        long bytes,
        ref long reservedExtractionBytes)
    {
        if (reservedExtractionBytes > MaxTotalModPackageExtractionSize - bytes)
        {
            throw CreateTotalExtractionTooLargeException(entry, reservedExtractionBytes + bytes);
        }

        reservedExtractionBytes += bytes;
    }

    private static InvalidOperationException CreateTotalExtractionTooLargeException(
        ZipArchiveEntry entry,
        long totalBytes)
    {
        return new InvalidOperationException(
            $"Zip package exceeds the maximum allowed total uncompressed size while extracting {entry.FullName}: " +
            $"{totalBytes} bytes > {MaxTotalModPackageExtractionSize} bytes.");
    }
}
