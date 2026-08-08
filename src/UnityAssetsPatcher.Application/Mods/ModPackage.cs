using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModPackage : IDisposable
{
    public IReadOnlyDictionary<string, string> PatchSourcePaths { get; }
    public IReadOnlyList<string> AppliedOptionalGroups { get; }
    public ModManifest SourceManifest { get; }
    public ModManifest EffectiveManifest { get; }

    private readonly ModPackageArchive _archive;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly string? _temporaryDirectory;

    private ModPackage(
        ModManifest sourceManifest,
        ModManifest effectiveManifest,
        IReadOnlyList<string> appliedOptionalGroups,
        IReadOnlyDictionary<string, string> patchSourcePaths,
        ModPackageArchive archive,
        IFileSystemOperations fileSystemOperations,
        string? temporaryDirectory)
    {
        SourceManifest = sourceManifest;
        EffectiveManifest = effectiveManifest;
        AppliedOptionalGroups = appliedOptionalGroups;
        PatchSourcePaths = patchSourcePaths;
        _archive = archive;
        _fileSystemOperations = fileSystemOperations;
        _temporaryDirectory = temporaryDirectory;
    }

    public static OperationResult<ModPackage> Open(
        string modPackagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        IFileSystemOperations fileSystemOperations,
        StepTimer timings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPackagePath);
        ArgumentNullException.ThrowIfNull(selectedOptionalGroups);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(timings);

        string modPackageFullPath = Path.GetFullPath(modPackagePath);
        OperationResult<ModPackageArchive> archiveResult = ModPackageArchive.OpenRead(
            modPackageFullPath,
            fileSystemOperations);

        if (archiveResult is OperationFailed<ModPackageArchive> archiveFailure)
        {
            return new OperationFailed<ModPackage>(archiveFailure.Error);
        }

        ModPackageArchive archive = ((OperationSucceeded<ModPackageArchive>)archiveResult).Value;

        try
        {
            OperationResult<ModPackageContent> contentResult = timings.Measure(
                "read-package",
                () => ReadContent(archive, selectedOptionalGroups));

            if (contentResult is OperationFailed<ModPackageContent> contentFailure)
            {
                archive.Dispose();

                return new OperationFailed<ModPackage>(contentFailure.Error);
            }

            ModPackageContent content = ((OperationSucceeded<ModPackageContent>)contentResult).Value;
            OperationResult<PreparedSources> sourcesResult = timings.Measure(
                "prepare-sources",
                () => ExtractPatchSources(archive, fileSystemOperations, content.Selection.EffectiveManifest));

            if (sourcesResult is OperationFailed<PreparedSources> sourcesFailure)
            {
                archive.Dispose();

                return new OperationFailed<ModPackage>(sourcesFailure.Error);
            }

            PreparedSources sources = ((OperationSucceeded<PreparedSources>)sourcesResult).Value;
            var package = new ModPackage(
                content.SourceManifest,
                content.Selection.EffectiveManifest,
                content.Selection.AppliedOptionalGroups,
                sources.Paths,
                archive,
                fileSystemOperations,
                sources.TemporaryDirectory);

            return new OperationSucceeded<ModPackage>(package);
        }
        catch
        {
            archive.Dispose();

            throw;
        }
    }

    public OperationResult<long> CopyPayloadFile(string source, string destinationPath)
    {
        return _archive.CopyEntryToNewFile(source, destinationPath);
    }

    public void Dispose()
    {
        try
        {
            _archive.Dispose();
        }
        finally
        {
            if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
            {
                _fileSystemOperations.DeleteDirectoryTree(_temporaryDirectory);
            }
        }
    }

    private static OperationResult<ModPackageContent> ReadContent(
        ModPackageArchive archive,
        IReadOnlyList<string> selectedOptionalGroups)
    {
        OperationResult<byte[]> manifestBytesResult = archive.ReadManifest();

        if (manifestBytesResult is OperationFailed<byte[]> manifestBytesFailure)
        {
            return new OperationFailed<ModPackageContent>(manifestBytesFailure.Error);
        }

        byte[] manifestBytes = ((OperationSucceeded<byte[]>)manifestBytesResult).Value;
        OperationResult<ModManifest> manifestResult = ModManifestParser.Parse(manifestBytes);

        if (manifestResult is OperationFailed<ModManifest> manifestFailure)
        {
            return new OperationFailed<ModPackageContent>(manifestFailure.Error);
        }

        ModManifest manifest = ((OperationSucceeded<ModManifest>)manifestResult).Value;
        OperationResult<ModManifestSelection> selectionResult = ModManifestOptionalSelector.Select(
            manifest,
            selectedOptionalGroups);

        if (selectionResult is OperationFailed<ModManifestSelection> selectionFailure)
        {
            return new OperationFailed<ModPackageContent>(selectionFailure.Error);
        }

        ModManifestSelection selection = ((OperationSucceeded<ModManifestSelection>)selectionResult).Value;

        return new OperationSucceeded<ModPackageContent>(new ModPackageContent(manifest, selection));
    }

    private static OperationResult<PreparedSources> ExtractPatchSources(
        ModPackageArchive archive,
        IFileSystemOperations fileSystemOperations,
        ModManifest manifest)
    {
        string[] replacementSources =
        [
            .. manifest.Patches
                .Select(patch => patch.ReplaceAsset?.SourceAssetsFile)
                .OfType<string>()
                .Select(source => source.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        if (replacementSources.Length == 0)
        {
            var emptySources = new PreparedSources(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                null);

            return new OperationSucceeded<PreparedSources>(emptySources);
        }

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher.{Guid.NewGuid():N}");

        try
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string source in replacementSources)
            {
                string destinationPath = ResolveUnderDirectory(fileSystemOperations, temporaryDirectory, source);
                OperationResult<long> copyResult = archive.CopyEntryToNewFile(source, destinationPath);

                if (copyResult is OperationFailed<long> copyFailure)
                {
                    if (Directory.Exists(temporaryDirectory))
                    {
                        fileSystemOperations.DeleteDirectoryTree(temporaryDirectory);
                    }

                    return new OperationFailed<PreparedSources>(copyFailure.Error);
                }

                paths[source] = destinationPath;
            }

            return new OperationSucceeded<PreparedSources>(new PreparedSources(paths, temporaryDirectory));
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                fileSystemOperations.DeleteDirectoryTree(temporaryDirectory);
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
            throw new InvalidDataException(
                $"Zip payload source cannot escape its extraction directory: {relativePath}");
        }

        return fullPath;
    }

    private sealed record ModPackageContent(ModManifest SourceManifest, ModManifestSelection Selection);

    private sealed record PreparedSources(
        IReadOnlyDictionary<string, string> Paths,
        string? TemporaryDirectory);
}
