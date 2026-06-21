using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Modules;

public sealed record PayloadPlan(string PackagePath, ZipArchive PackageArchive, IReadOnlyList<PayloadFilePlan> Files);

public sealed record PayloadFilePlan(string Source, string DestinationPath);

public sealed record PayloadPreview(IReadOnlyList<PayloadFilePreview> Files);

public sealed record PayloadFilePreview(string Source, string DestinationPath, bool WillCopy);

public sealed record PayloadCopyResult(IReadOnlyList<PayloadCopiedFile> Files);

public sealed record PayloadCopiedFile(string Source, string DestinationPath);

public sealed class ModPackage : IDisposable
{
    public IReadOnlyDictionary<string, string> SourceAssetsPaths { get; }
    public ModManifest Manifest { get; }
    private ZipArchive Archive { get; }
    public string GameDirectory { get; }
    public string PackagePath { get; }

    private readonly string? _temporaryDirectory;

    private ModPackage(
        string packagePath,
        ZipArchive archive,
        ModManifest manifest,
        string gameDirectory,
        IReadOnlyDictionary<string, string> sourceAssetsPaths,
        string? temporaryDirectory)
    {
        PackagePath = packagePath;
        Archive = archive;
        Manifest = manifest;
        GameDirectory = gameDirectory;
        SourceAssetsPaths = sourceAssetsPaths;
        _temporaryDirectory = temporaryDirectory;
    }

    public static ModPackage Load(
        string modPackagePath,
        string? gameDirectory,
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive,
        WorkflowTiming timings)
    {
        string modPackageFullPath = Path.GetFullPath(modPackagePath);

        ZipArchive? archive = null;

        bool modPackageExists = File.Exists(modPackageFullPath);

        if (!modPackageExists)
        {
            throw new FileNotFoundException($"Mod zip file not found: {modPackageFullPath}", modPackageFullPath);
        }

        try
        {
            ModManifest modManifest = timings.MeasureReadPackage(() =>
            {
                archive = openPackageArchive(modPackageFullPath);

                JsonElement manifestElement = ModManifestJsonReader.ReadFromZipArchive(archive, modPackageFullPath);

                return manifestReader.Load(manifestElement);
            });

            string gameDirectoryPath = ResolveGameDirectory(gameDirectory, modManifest, gameDirectoryResolver);

            if (archive is null)
            {
                throw new InvalidOperationException("Package archive was not opened while reading the manifest.");
            }

            ZipArchive sourceArchive = archive;

            (var sourceAssetsPaths, string? temporaryDirectory) =
                timings.MeasurePrepareSources(() =>
                    ExtractSourceAssets(modPackageFullPath, modManifest, sourceArchive));

            archive = null;

            return new ModPackage(
                modPackageFullPath,
                sourceArchive,
                modManifest,
                gameDirectoryPath,
                sourceAssetsPaths,
                temporaryDirectory);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    public PayloadPlan PlanPayload(TargetAssetSet targets, bool requireAvailableDestination)
    {
        if (Manifest.Files.Count == 0)
        {
            return new PayloadPlan(PackagePath, Archive, []);
        }

        string payloadDirectory = ResolvePayloadDirectory(targets.AssetsFilePaths);
        var files = new List<PayloadFilePlan>();

        foreach (ManifestFile file in Manifest.Files)
        {
            string entryPath = PackageArchive.NormalizeEntryPath(file.Source);
            PackageArchive.FindFileEntry(Archive, entryPath, PackagePath);
            string destinationPath = Path.Combine(payloadDirectory, PackageArchive.GetFileName(entryPath));

            if (requireAvailableDestination && File.Exists(destinationPath))
            {
                throw new IOException($"Payload file already exists: {destinationPath}");
            }

            files.Add(new PayloadFilePlan(entryPath, destinationPath));
        }

        return new PayloadPlan(PackagePath, Archive, files);
    }

    public static PayloadPreview PreviewPayload(PayloadPlan plan)
    {
        var files = plan.Files
            .Select(file => new PayloadFilePreview(
                file.Source,
                file.DestinationPath,
                !File.Exists(file.DestinationPath)))
            .ToArray();

        return new PayloadPreview(files);
    }

    public static PayloadCopyResult CopyPayload(PayloadPlan plan, WorkflowTiming timings)
    {
        return timings.MeasureCopyFiles(() =>
        {
            if (plan.Files.Count == 0)
            {
                return new PayloadCopyResult([]);
            }

            var results = new List<PayloadCopiedFile>();

            foreach (PayloadFilePlan file in plan.Files)
            {
                ZipArchiveEntry entry =
                    PackageArchive.FindFileEntry(plan.PackageArchive, file.Source, plan.PackagePath);
                PackageArchive.CopyEntryToNewFile(
                    entry,
                    file.DestinationPath,
                    PackageArchive.MaxEntryUncompressedBytes);
                results.Add(new PayloadCopiedFile(file.Source, file.DestinationPath));
            }

            return new PayloadCopyResult(results);
        });
    }

    public void Dispose()
    {
        if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }

        Archive.Dispose();
    }

    private static (IReadOnlyDictionary<string, string> Paths, string? TemporaryDirectory) ExtractSourceAssets(
        string packagePath,
        ModManifest manifest,
        ZipArchive archive)
    {
        string[] replacementSources = manifest.Patches
            .Select(patch => patch.ReplaceFrom?.AssetsFilePath)
            .OfType<string>()
            .Select(PackageArchive.NormalizeEntryPath)
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
                ZipArchiveEntry entry = PackageArchive.FindFileEntry(archive, source, packagePath);
                string destinationPath = PackageArchive.ResolveUnderDirectory(temporaryDirectory, source);
                PackageArchive.CopyEntryToNewFile(
                    entry,
                    destinationPath,
                    PackageArchive.MaxEntryUncompressedBytes);
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

    private static string ResolveGameDirectory(
        string? gameDirectory,
        ModManifest manifest,
        GameDirectoryResolver gameDirectoryResolver)
    {
        if (!string.IsNullOrWhiteSpace(gameDirectory))
        {
            string fullGameDirectory = Path.GetFullPath(gameDirectory);

            return Directory.Exists(fullGameDirectory)
                ? fullGameDirectory
                : throw new DirectoryNotFoundException($"Game directory not found: {fullGameDirectory}");
        }

        if (string.IsNullOrWhiteSpace(manifest.Game))
        {
            throw new DirectoryNotFoundException(
                "Game directory was not provided and manifest does not contain a 'game' property.");
        }

        string? resolvedDirectory = gameDirectoryResolver.Resolve(manifest.Game);

        return resolvedDirectory ?? throw new DirectoryNotFoundException(
            $"Game directory could not be resolved for manifest game: {manifest.Game}");
    }

    private static string ResolvePayloadDirectory(IEnumerable<string> targetAssetsFilePaths)
    {
        string[] targetDirectories = targetAssetsFilePaths
            .Select(path => Path.GetDirectoryName(Path.GetFullPath(path)) ??
                            throw new InvalidOperationException(
                                $"Cannot resolve directory for assets file: {path}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return targetDirectories.Length switch
        {
            1 => targetDirectories[0],
            0 => throw new InvalidOperationException("Payload files require at least one patch target."),
            _ => throw new InvalidOperationException(
                "Payload files require all patch targets to resolve to the same directory.")
        };
    }
}
