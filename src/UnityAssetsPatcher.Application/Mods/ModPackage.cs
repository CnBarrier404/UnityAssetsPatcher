using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModPackage : IDisposable
{
    public IReadOnlyDictionary<string, string> PatchSourcePaths { get; }
    public IReadOnlyList<string> AppliedOptionalGroups { get; }
    public ModManifest SourceManifest { get; }
    public ModManifest EffectiveManifest { get; }

    private readonly IReadOnlyDictionary<string, string> _entryPaths;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly string? _temporaryDirectory;

    private ModPackage(
        ModManifest sourceManifest,
        ModManifest effectiveManifest,
        IReadOnlyList<string> appliedOptionalGroups,
        IReadOnlyDictionary<string, string> patchSourcePaths,
        IReadOnlyDictionary<string, string> entryPaths,
        IFileSystemOperations fileSystemOperations,
        string? temporaryDirectory)
    {
        SourceManifest = sourceManifest;
        EffectiveManifest = effectiveManifest;
        AppliedOptionalGroups = appliedOptionalGroups;
        PatchSourcePaths = patchSourcePaths;
        _entryPaths = entryPaths;
        _fileSystemOperations = fileSystemOperations;
        _temporaryDirectory = temporaryDirectory;
    }

    public static OperationResult<ModPackage> Open(
        string modPackagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        IPackageReader packageReader,
        IFileSystemOperations fileSystemOperations,
        StepTimer timings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPackagePath);
        ArgumentNullException.ThrowIfNull(selectedOptionalGroups);
        ArgumentNullException.ThrowIfNull(packageReader);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(timings);

        string modPackageFullPath = Path.GetFullPath(modPackagePath);
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher.{Guid.NewGuid():N}");

        try
        {
            PackageContent packageContent = timings.Measure(
                "read-package",
                () => packageReader.Read(modPackageFullPath, temporaryDirectory));
            OperationResult<ModPackageContent> contentResult = timings.Measure(
                "parse-package",
                () => ReadContent(packageContent.Manifest, selectedOptionalGroups));

            if (contentResult is OperationFailed<ModPackageContent> contentFailure)
            {
                DeleteTemporaryDirectory(fileSystemOperations, temporaryDirectory);

                return new OperationFailed<ModPackage>(contentFailure.Error);
            }

            ModPackageContent content = ((OperationSucceeded<ModPackageContent>)contentResult).Value;
            IReadOnlyDictionary<string, string> patchSourcePaths = timings.Measure(
                "prepare-sources",
                () => SelectPatchSourcePaths(packageContent.EntryPaths, content.Selection.EffectiveManifest));
            var package = new ModPackage(
                content.SourceManifest,
                content.Selection.EffectiveManifest,
                content.Selection.AppliedOptionalGroups,
                patchSourcePaths,
                packageContent.EntryPaths,
                fileSystemOperations,
                temporaryDirectory);

            return new OperationSucceeded<ModPackage>(package);
        }
        catch
        {
            DeleteTemporaryDirectory(fileSystemOperations, temporaryDirectory);

            throw;
        }
    }

    public void CopyPayloadFile(string source, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        string normalizedSource = source.Replace('\\', '/');

        if (!_entryPaths.TryGetValue(normalizedSource, out string? extractedPath))
        {
            throw new InvalidDataException($"Package entry does not exist: {normalizedSource}");
        }

        _fileSystemOperations.CopyFileAtomically(extractedPath, destinationPath, FileDestinationMode.CreateNew);
    }

    public void Dispose()
    {
        DeleteTemporaryDirectory(_fileSystemOperations, _temporaryDirectory);
    }

    private static OperationResult<ModPackageContent> ReadContent(
        byte[] manifestBytes,
        IReadOnlyList<string> selectedOptionalGroups)
    {
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

    private static IReadOnlyDictionary<string, string> SelectPatchSourcePaths(
        IReadOnlyDictionary<string, string> entryPaths,
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
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string source in replacementSources)
        {
            if (!entryPaths.TryGetValue(source, out string? extractedPath))
            {
                throw new InvalidDataException($"Package entry does not exist: {source}");
            }

            paths[source] = extractedPath;
        }

        return paths;
    }

    private static void DeleteTemporaryDirectory(
        IFileSystemOperations fileSystemOperations,
        string? temporaryDirectory)
    {
        if (temporaryDirectory is not null && Directory.Exists(temporaryDirectory))
        {
            fileSystemOperations.DeleteDirectoryTree(temporaryDirectory);
        }
    }

    private sealed record ModPackageContent(ModManifest SourceManifest, ModManifestSelection Selection);
}
