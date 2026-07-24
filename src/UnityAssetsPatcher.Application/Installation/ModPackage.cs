using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Abstractions.IO;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Installation;

public sealed class ModPackage : IDisposable
{
    public IReadOnlyDictionary<string, string> PatchSourcePaths { get; }
    public IReadOnlyList<ManifestOptionalGroup> OptionalGroups { get; }
    public IReadOnlyList<string> AppliedOptionalGroups { get; }
    public ModManifest Manifest { get; }

    private readonly ModPackageArchive _archive;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly string? _temporaryDirectory;
    private long _reservedUncompressedBytes;

    private ModPackage(
        ModManifest manifest,
        IReadOnlyList<ManifestOptionalGroup> optionalGroups,
        IReadOnlyList<string> appliedOptionalGroups,
        IReadOnlyDictionary<string, string> patchSourcePaths,
        ModPackageArchive archive,
        IFileSystemOperations fileSystemOperations,
        string? temporaryDirectory,
        long reservedUncompressedBytes)
    {
        Manifest = manifest;
        OptionalGroups = optionalGroups;
        AppliedOptionalGroups = appliedOptionalGroups;
        PatchSourcePaths = patchSourcePaths;
        _archive = archive;
        _fileSystemOperations = fileSystemOperations;
        _temporaryDirectory = temporaryDirectory;
        _reservedUncompressedBytes = reservedUncompressedBytes;
    }

    public static ModPackage Open(
        string modPackagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        ModManifestReader manifestReader,
        IFileSystemOperations fileSystemOperations,
        StepTimer timings)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        string modPackageFullPath = Path.GetFullPath(modPackagePath);
        long reservedUncompressedBytes = 0;
        var packageArchive = new ModPackageArchive(modPackageFullPath, fileSystemOperations);

        ZipArchive? archive = null;

        if (!File.Exists(modPackageFullPath))
        {
            throw new FileNotFoundException($"Mod not found: {modPackageFullPath}");
        }

        try
        {
            ModManifest manifest = timings.Measure("read-package", () =>
            {
                archive = packageArchive.OpenRead();

                JsonElement manifestElement = ModManifestJsonReader.ReadFromZipArchive(archive, modPackageFullPath);

                return manifestReader.Load(manifestElement);
            });

            ModManifest effectiveManifest = manifest.SelectOptional(selectedOptionalGroups);
            string[] appliedOptionalGroups = ResolveAppliedOptionalGroups(manifest.Optional, selectedOptionalGroups);

            if (archive is null)
            {
                throw new InvalidOperationException("Mod package was not opened while reading the manifest.");
            }

            (var patchSourcePaths, string? temporaryDirectory) =
                timings.Measure("prepare-sources", () =>
                    ExtractPatchSources(
                        packageArchive,
                        fileSystemOperations,
                        effectiveManifest,
                        archive,
                        ref reservedUncompressedBytes));

            return new ModPackage(
                effectiveManifest,
                manifest.Optional,
                appliedOptionalGroups,
                patchSourcePaths,
                packageArchive,
                fileSystemOperations,
                temporaryDirectory,
                reservedUncompressedBytes);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    public void CopyPayloadFile(string source, string destinationPath)
    {
        using ZipArchive archive = _archive.OpenRead();
        ZipArchiveEntry entry = _archive.FindRequiredFileEntry(archive, source);

        _archive.CopyEntryToNewFile(entry, destinationPath, ref _reservedUncompressedBytes);
    }

    public void Dispose()
    {
        if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
        {
            _fileSystemOperations.DeleteDirectory(_temporaryDirectory);
        }
    }

    private static string[] ResolveAppliedOptionalGroups(
        IReadOnlyList<ManifestOptionalGroup> optionalGroups,
        IReadOnlyList<string> selectedOptionalGroups)
    {
        if (selectedOptionalGroups.Count == 0)
        {
            return [];
        }

        var selected = new HashSet<string>(selectedOptionalGroups, StringComparer.OrdinalIgnoreCase);

        return optionalGroups
            .Where(group => selected.Contains(group.Name))
            .Select(group => group.Name)
            .ToArray();
    }

    private static (IReadOnlyDictionary<string, string> Paths, string? TemporaryDirectory) ExtractPatchSources(
        ModPackageArchive packageArchive,
        IFileSystemOperations fileSystemOperations,
        ModManifest manifest,
        ZipArchive archive,
        ref long reservedUncompressedBytes)
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
                ZipArchiveEntry entry = packageArchive.FindRequiredFileEntry(archive, source);
                string destinationPath = ResolveUnderDirectory(fileSystemOperations, temporaryDirectory, source);

                packageArchive.CopyEntryToNewFile(entry, destinationPath, ref reservedUncompressedBytes);
                paths[source] = destinationPath;
            }

            return (paths, temporaryDirectory);
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                fileSystemOperations.DeleteDirectory(temporaryDirectory);
            }

            throw;
        }
    }

    private static string ResolveUnderDirectory(
        IFileSystemOperations fileSystemOperations,
        string rootDirectory,
        string relativePath)
    {
        string fullRootDirectory = Path.GetFullPath(rootDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(
            fullRootDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fileSystemOperations.IsPathWithinDirectory(fullPath, fullRootDirectory))
        {
            throw new InvalidOperationException(
                $"Zip payload source cannot escape its extraction directory: {relativePath}");
        }

        return fullPath;
    }
}
