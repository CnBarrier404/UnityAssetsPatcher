using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModPackage : IDisposable
{
    public IReadOnlyDictionary<string, string> PatchSourcePaths { get; }
    public IReadOnlyList<string> AppliedOptionalGroups { get; }
    public ModManifest SourceManifest { get; }
    public ModManifest EffectiveManifest { get; }

    private readonly ModPackageSession _modPackageSession;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly string? _temporaryDirectory;

    private ModPackage(
        ModManifest sourceManifest,
        ModManifest effectiveManifest,
        IReadOnlyList<string> appliedOptionalGroups,
        IReadOnlyDictionary<string, string> patchSourcePaths,
        ModPackageSession modPackageSession,
        IFileSystemOperations fileSystemOperations,
        string? temporaryDirectory)
    {
        SourceManifest = sourceManifest;
        EffectiveManifest = effectiveManifest;
        AppliedOptionalGroups = appliedOptionalGroups;
        PatchSourcePaths = patchSourcePaths;
        _modPackageSession = modPackageSession;
        _fileSystemOperations = fileSystemOperations;
        _temporaryDirectory = temporaryDirectory;
    }

    public static async Task<OperationResult<ModPackage>> OpenAsync(
        string modPackagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        ModPackageReader modPackageReader,
        IFileSystemOperations fileSystemOperations,
        StepTimer timings,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPackagePath);
        ArgumentNullException.ThrowIfNull(selectedOptionalGroups);
        ArgumentNullException.ThrowIfNull(modPackageReader);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(timings);
        cancellationToken.ThrowIfCancellationRequested();

        string modPackageFullPath = Path.GetFullPath(modPackagePath);
        OperationResult<ModPackageSession> sessionResult = await modPackageReader
            .OpenAsync(modPackageFullPath, cancellationToken)
            .ConfigureAwait(false);

        if (sessionResult is OperationFailed<ModPackageSession> sessionFailure)
        {
            return new OperationFailed<ModPackage>(sessionFailure.Error);
        }

        ModPackageSession modPackageSession = ((OperationSucceeded<ModPackageSession>)sessionResult).Value;

        try
        {
            OperationResult<ModPackageContent> contentResult = await timings.MeasureAsync(
                    "read-package",
                    () => ReadContentAsync(
                        modPackageSession,
                        modPackageFullPath,
                        selectedOptionalGroups,
                        cancellationToken))
                .ConfigureAwait(false);

            if (contentResult is OperationFailed<ModPackageContent> contentFailure)
            {
                modPackageSession.Dispose();

                return new OperationFailed<ModPackage>(contentFailure.Error);
            }

            ModPackageContent content = ((OperationSucceeded<ModPackageContent>)contentResult).Value;
            OperationResult<PreparedSources> sourcesResult = await timings.MeasureAsync(
                    "prepare-sources",
                    () => ExtractPatchSourcesAsync(
                        modPackageSession,
                        fileSystemOperations,
                        content.Selection.EffectiveManifest,
                        cancellationToken))
                .ConfigureAwait(false);

            if (sourcesResult is OperationFailed<PreparedSources> sourcesFailure)
            {
                modPackageSession.Dispose();

                return new OperationFailed<ModPackage>(sourcesFailure.Error);
            }

            PreparedSources sources = ((OperationSucceeded<PreparedSources>)sourcesResult).Value;
            var package = new ModPackage(
                content.SourceManifest,
                content.Selection.EffectiveManifest,
                content.Selection.AppliedOptionalGroups,
                sources.Paths,
                modPackageSession,
                fileSystemOperations,
                sources.TemporaryDirectory);

            return new OperationSucceeded<ModPackage>(package);
        }
        catch
        {
            modPackageSession.Dispose();

            throw;
        }
    }

    public Task<OperationResult<long>> CopyPayloadFileAsync(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return _modPackageSession.CopyEntryToNewFileAsync(source, destinationPath, cancellationToken);
    }

    public void Dispose()
    {
        try
        {
            _modPackageSession.Dispose();
        }
        finally
        {
            if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
            {
                _fileSystemOperations.DeleteDirectoryTree(_temporaryDirectory);
            }
        }
    }

    private static async Task<OperationResult<ModPackageContent>> ReadContentAsync(
        ModPackageSession modPackageSession,
        string packagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        CancellationToken cancellationToken)
    {
        OperationResult<byte[]> bytesResult = await modPackageSession.ReadManifestAsync(cancellationToken)
            .ConfigureAwait(false);

        if (bytesResult is OperationFailed<byte[]> bytesFailure)
        {
            return new OperationFailed<ModPackageContent>(bytesFailure.Error);
        }

        byte[] manifestBytes = ((OperationSucceeded<byte[]>)bytesResult).Value;
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
        ModFile? missingFile = selection.EffectiveManifest.Files.FirstOrDefault(file =>
            !modPackageSession.ContainsEntry(file.Source));

        if (missingFile is not null)
        {
            return new OperationFailed<ModPackageContent>(new OperationError(
                ModPackageErrorCodes.MissingEntry,
                new Dictionary<string, object?>
                {
                    ["package_path"] = packagePath,
                    ["entry_path"] = missingFile.Source,
                }));
        }

        return new OperationSucceeded<ModPackageContent>(new ModPackageContent(manifest, selection));
    }

    private static async Task<OperationResult<PreparedSources>> ExtractPatchSourcesAsync(
        ModPackageSession modPackageSession,
        IFileSystemOperations fileSystemOperations,
        ModManifest manifest,
        CancellationToken cancellationToken)
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
                OperationResult<long> copyResult = await modPackageSession
                    .CopyEntryToNewFileAsync(source, destinationPath, cancellationToken)
                    .ConfigureAwait(false);

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
                $"Package payload source cannot escape its extraction directory: {relativePath}");
        }

        return fullPath;
    }

    private sealed record ModPackageContent(ModManifest SourceManifest, ModManifestSelection Selection);

    private sealed record PreparedSources(
        IReadOnlyDictionary<string, string> Paths,
        string? TemporaryDirectory);
}
