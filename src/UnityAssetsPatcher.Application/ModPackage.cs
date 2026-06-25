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

    private const long MaxModPackageExtractionSize = 2L * 1024L * 1024L * 1024L; // 2GB
    private const int CopyBufferSize = 81920;

    private ModPackage(
        string packagePath,
        ModManifest manifest,
        IReadOnlyList<ManifestOptionalGroup> availableOptional,
        IReadOnlyDictionary<string, string> sourceAssetsPaths,
        Func<string, ZipArchive> openPackageArchive,
        string? temporaryDirectory)
    {
        PackagePath = packagePath;
        Manifest = manifest;
        AvailableOptional = availableOptional;
        SourceAssetsPaths = sourceAssetsPaths;
        _openPackageArchive = openPackageArchive;
        _temporaryDirectory = temporaryDirectory;
    }

    public static ModPackage Load(
        string modPackagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        ModManifestReader manifestReader,
        Func<string, ZipArchive> openPackageArchive,
        StepTimer timings)
    {
        string modPackageFullPath = Path.GetFullPath(modPackagePath);

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
                    ExtractSourceAssets(modPackageFullPath, effectiveManifest, archive));

            return new ModPackage(
                modPackageFullPath,
                effectiveManifest,
                manifest.Optional,
                sourceAssetsPaths,
                openPackageArchive,
                temporaryDirectory);
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
        CopyEntryToNewFile(entry, destinationPath);
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
        ZipArchive archive)
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

                CopyEntryToNewFile(entry, destinationPath);
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

    private static void CopyEntryToNewFile(ZipArchiveEntry entry, string destinationPath)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);

        if (entry.Length > MaxModPackageExtractionSize)
        {
            throw CreateEntryTooLargeException(entry, entry.Length);
        }

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
                    if (copiedBytes > MaxModPackageExtractionSize - bytesRead)
                    {
                        throw CreateEntryTooLargeException(entry, copiedBytes + bytesRead);
                    }

                    output.Write(buffer, 0, bytesRead);
                    copiedBytes += bytesRead;
                }
            }

            File.Move(tempPath, destinationPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static InvalidOperationException CreateEntryTooLargeException(
        ZipArchiveEntry entry,
        long entryBytes)
    {
        return new InvalidOperationException(
            $"Zip entry exceeds the maximum allowed uncompressed size: {entry.FullName} " +
            $"({entryBytes} bytes > {MaxModPackageExtractionSize} bytes).");
    }
}
