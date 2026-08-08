using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Composition;

public sealed class ModComposer
{
    private readonly ICompositionRepository _compositionRepository;
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly IAssetsAccessScopeFactory _assetsAccessScopeFactory;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IReadOnlyList<IFieldPatchOperationHandler> _operationHandlers;
    private readonly TrustedPathResolver _pathResolver;
    private readonly ILogger<ModComposer> _logger;

    public ModComposer(
        ICompositionRepository compositionRepository,
        TargetAssetResolver targetAssetResolver,
        IAssetsAccessScopeFactory assetsAccessScopeFactory,
        IFileSystemOperations fileSystemOperations,
        IEnumerable<IFieldPatchOperationHandler> operationHandlers,
        ILogger<ModComposer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(compositionRepository);
        ArgumentNullException.ThrowIfNull(targetAssetResolver);
        ArgumentNullException.ThrowIfNull(assetsAccessScopeFactory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(operationHandlers);

        _compositionRepository = compositionRepository;
        _targetAssetResolver = targetAssetResolver;
        _assetsAccessScopeFactory = assetsAccessScopeFactory;
        _fileSystemOperations = fileSystemOperations;
        _operationHandlers = operationHandlers.ToArray();
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
        _logger = logger ?? NullLogger<ModComposer>.Instance;
    }

    public CompositionOutcome Compose(CompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string gameDirectory = _pathResolver.ResolveExistingDirectory(request.GameDirectory);
        string workingDirectory = _pathResolver.ResolveExistingDirectory(request.WorkingDirectory);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(_pathResolver, gameDirectory);
        var layers = SelectLayers(request, fingerprint);
        BaseCatalog baseCatalog = _compositionRepository.BaseSnapshots.ReadCatalog(fingerprint);
        string compositionDirectory = CreateCompositionDirectory(workingDirectory);

        using IAssetsAccessScope assetsAccessScope = _assetsAccessScopeFactory.CreateScope();
        PatchPlanner patchPlanner = CreatePatchPlanner(assetsAccessScope.Reader);
        var outputWriter = new PatchOutputWriter(assetsAccessScope.Writer, _fileSystemOperations);
        var results = new List<CompositionFileResult>(request.Files.Count);

        for (int fileIndex = 0; fileIndex < request.Files.Count; fileIndex++)
        {
            CompositionFileTarget file = request.Files[fileIndex];
            FileCompositionAttempt attempt = file.Kind switch
            {
                RepositoryFileKind.Assets => ComposeAssetsFile(
                    gameDirectory,
                    fingerprint,
                    baseCatalog,
                    layers,
                    compositionDirectory,
                    file,
                    fileIndex,
                    patchPlanner,
                    outputWriter,
                    request.LayerPackagePaths),
                RepositoryFileKind.Payload => ComposePayloadFile(
                    gameDirectory,
                    fingerprint,
                    baseCatalog,
                    layers,
                    compositionDirectory,
                    file,
                    fileIndex,
                    request.LayerPackagePaths),
                _ => throw new ArgumentOutOfRangeException(nameof(file.Kind), file.Kind, "Unsupported file kind."),
            };

            if (attempt.Failure is not null)
            {
                return new CompositionFailed(attempt.Failure);
            }

            results.Add(attempt.Result ?? throw new InvalidOperationException(
                "Composition did not produce a result or failure."));
        }

        return new CompositionSucceeded(new CompositionResult(results));
    }

    private FileCompositionAttempt ComposeAssetsFile(
        string gameDirectory,
        string fingerprint,
        BaseCatalog baseCatalog,
        IReadOnlyList<LayerRecord> layers,
        string compositionDirectory,
        CompositionFileTarget file,
        int fileIndex,
        PatchPlanner patchPlanner,
        PatchOutputWriter outputWriter,
        IReadOnlyDictionary<string, string> layerPackagePaths)
    {
        BaseFileEntry baseEntry = baseCatalog.AssetsFiles.FirstOrDefault(entry =>
                                      TrustedPath.PathComparer.Equals(entry.RelativePath, file.RelativePath)) ??
                                  throw new InvalidDataException(
                                      $"Base catalog does not contain the assets file: {file.RelativePath}");

        _compositionRepository.BaseSnapshots.VerifyFile(fingerprint, file.RelativePath, baseEntry.Integrity);
        string currentPath = _compositionRepository.BaseSnapshots.ResolveFilePath(fingerprint, file.RelativePath);
        currentPath = PrepareBaseFile(compositionDirectory, RepositoryFileKind.Assets, fileIndex, file.RelativePath,
            currentPath);

        int stage = 1;

        foreach (LayerRecord layer in layers)
        {
            using ModPackage package = OpenLayerPackage(layer, layerPackagePaths);
            TargetAssetSet targets = _targetAssetResolver.Execute(
                gameDirectory,
                package.EffectiveManifest,
                new StepTimer());
            TargetAsset? target = FindTargetAsset(gameDirectory, targets, file.RelativePath);

            if (target is null)
            {
                _logger.LogDebug(
                    "Layer {LayerId} did not resolve an assets target for {RelativePath}; its stored target snapshot was ignored.",
                    layer.Id,
                    file.RelativePath);

                continue;
            }

            PatchPlanningResult planning = patchPlanner.Plan(new PatchPlanningRequest(
                currentPath,
                target.Patches,
                package.PatchSourcePaths));

            if (!planning.CanApply)
            {
                PatchDiagnostic diagnostic = planning.Diagnostic ?? throw new InvalidOperationException(
                    "Patch planning failed without a diagnostic.");
                PatchDiagnostic compositionDiagnostic = diagnostic with { AssetsFilePath = file.RelativePath };

                return new FileCompositionAttempt(
                    null,
                    new CompositionFailure(layer.Id, file.RelativePath, [compositionDiagnostic]));
            }

            string outputPath = CreateWorkPath(
                compositionDirectory,
                RepositoryFileKind.Assets,
                fileIndex,
                stage,
                file.RelativePath);
            outputWriter.Write(currentPath, outputPath, planning.Plan!);
            EnsureRegularFile(outputPath, "Composed assets file");
            currentPath = outputPath;
            stage++;
        }

        return new FileCompositionAttempt(
            new CompositionFileResult(RepositoryFileKind.Assets, file.RelativePath, currentPath),
            null);
    }

    private FileCompositionAttempt ComposePayloadFile(
        string gameDirectory,
        string fingerprint,
        BaseCatalog baseCatalog,
        IReadOnlyList<LayerRecord> layers,
        string compositionDirectory,
        CompositionFileTarget file,
        int fileIndex,
        IReadOnlyDictionary<string, string> layerPackagePaths)
    {
        PayloadProvider? provider = FindPayloadProvider(
            gameDirectory,
            file.RelativePath,
            layers,
            layerPackagePaths);

        if (provider is not null)
        {
            using ModPackage package = OpenLayerPackage(provider.Layer, layerPackagePaths);
            string providerOutputPath = CreateWorkPath(
                compositionDirectory,
                RepositoryFileKind.Payload,
                fileIndex,
                0,
                file.RelativePath);
            _ = RequirePackageResult(package.CopyPayloadFile(provider.Source, providerOutputPath));
            EnsureRegularFile(providerOutputPath, "Composed payload file");

            return new FileCompositionAttempt(
                new CompositionFileResult(RepositoryFileKind.Payload, file.RelativePath, providerOutputPath),
                null);
        }

        PayloadBaseEntry baseEntry = baseCatalog.PayloadTargets.FirstOrDefault(entry =>
                                         TrustedPath.PathComparer.Equals(entry.RelativePath, file.RelativePath)) ??
                                     throw new InvalidDataException(
                                         $"Base catalog does not contain the payload file: {file.RelativePath}");

        if (baseEntry.BaseState == PayloadBaseState.Absent)
        {
            return new FileCompositionAttempt(
                new CompositionFileResult(RepositoryFileKind.Payload, file.RelativePath, null),
                null);
        }

        _compositionRepository.BaseSnapshots.VerifyFile(
            fingerprint,
            file.RelativePath,
            baseEntry.Integrity ?? throw new InvalidDataException(
                $"Present payload base entry does not contain integrity: {file.RelativePath}"));
        string basePath = _compositionRepository.BaseSnapshots.ResolveFilePath(fingerprint, file.RelativePath);
        string outputPath = PrepareBaseFile(
            compositionDirectory,
            RepositoryFileKind.Payload,
            fileIndex,
            file.RelativePath,
            basePath);

        return new FileCompositionAttempt(
            new CompositionFileResult(RepositoryFileKind.Payload, file.RelativePath, outputPath),
            null);
    }

    private PayloadProvider? FindPayloadProvider(
        string gameDirectory,
        string relativePath,
        IReadOnlyList<LayerRecord> layers,
        IReadOnlyDictionary<string, string> layerPackagePaths)
    {
        for (int index = layers.Count - 1; index >= 0; index--)
        {
            LayerRecord layer = layers[index];

            using ModPackage package = OpenLayerPackage(layer, layerPackagePaths);
            TargetAssetSet targets = _targetAssetResolver.Execute(
                gameDirectory,
                package.EffectiveManifest,
                new StepTimer());
            var payloadFiles = InstallPlanBuilder.PlanPayloadFiles(
                package.EffectiveManifest,
                targets);
            PayloadProvider? provider = FindPayloadProviderInLayer(
                gameDirectory,
                relativePath,
                layer,
                payloadFiles);

            if (provider is not null)
            {
                return provider;
            }
        }

        return null;
    }

    private PayloadProvider? FindPayloadProviderInLayer(
        string gameDirectory,
        string relativePath,
        LayerRecord layer,
        IReadOnlyList<InstallPayloadFilePlan> payloadFiles)
    {
        PayloadProvider? provider = null;

        foreach (InstallPayloadFilePlan payloadFile in payloadFiles)
        {
            string destinationRelativePath = ToGameRelativePath(gameDirectory, payloadFile.DestinationPath);

            if (!TrustedPath.PathComparer.Equals(destinationRelativePath, relativePath))
            {
                continue;
            }

            if (provider is not null)
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' contains multiple payload providers for '{relativePath}'.");
            }

            provider = new PayloadProvider(layer, payloadFile.Source);
        }

        return provider;
    }

    private ModPackage OpenLayerPackage(
        LayerRecord layer,
        IReadOnlyDictionary<string, string> layerPackagePaths)
    {
        string packagePath;

        if (layerPackagePaths.TryGetValue(layer.Id, out string? preparedPackagePath))
        {
            packagePath = TrustedPath.NormalizeAbsolutePath(preparedPackagePath);
            EnsureRegularFile(packagePath, "Prepared layer package");
            FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(packagePath);

            if (!layer.Package.Integrity.Matches(actual))
            {
                throw new InvalidDataException($"Layer package integrity does not match: {packagePath}");
            }
        }
        else
        {
            _compositionRepository.Layers.VerifyPackage(layer.Id);
            packagePath = _compositionRepository.Layers.ResolvePackagePath(layer.Id);
        }

        return RequirePackageResult(ModPackage.Open(
            packagePath,
            layer.OptionalGroups ?? [],
            _fileSystemOperations,
            new StepTimer()));
    }

    private IReadOnlyList<LayerRecord> SelectLayers(CompositionRequest request, string fingerprint)
    {
        var seen = new HashSet<string>(TrustedPath.PathComparer);
        var selected = new List<LayerRecord>(request.ActiveLayers.Count);

        foreach (LayerRecord layer in request.ActiveLayers)
        {
            if (!seen.Add(layer.Id))
            {
                throw new InvalidDataException($"Composition request contains duplicate layer: {layer.Id}");
            }

            if (!TrustedPath.PathComparer.Equals(layer.GameInstanceFingerprint, fingerprint))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' belongs to a different game instance.");
            }

            if (!layer.Enabled ||
                (request.ExcludedLayerId is not null &&
                 TrustedPath.PathComparer.Equals(layer.Id, request.ExcludedLayerId)))
            {
                continue;
            }

            selected.Add(layer);
        }

        return selected;
    }

    private TargetAsset? FindTargetAsset(
        string gameDirectory,
        TargetAssetSet targets,
        string relativePath)
    {
        string expectedPath = _pathResolver.ResolveWithinDirectory(gameDirectory, relativePath);

        return targets.Targets.FirstOrDefault(target => TrustedPath.PathsEqual(
            target.AssetsFilePath,
            expectedPath));
    }

    private string PrepareBaseFile(
        string compositionDirectory,
        RepositoryFileKind kind,
        int fileIndex,
        string relativePath,
        string sourcePath)
    {
        string outputPath = CreateWorkPath(compositionDirectory, kind, fileIndex, 0, relativePath);
        _fileSystemOperations.CopyFileAtomically(sourcePath, outputPath, FileDestinationMode.CreateNew);
        EnsureRegularFile(outputPath, "Composed base file");

        return outputPath;
    }

    private string CreateCompositionDirectory(string workingDirectory)
    {
        string compositionDirectory = Path.Combine(workingDirectory, $"composition-{Guid.NewGuid():N}");
        _fileSystemOperations.EnsureDirectory(compositionDirectory);

        return _pathResolver.ResolveExistingDirectory(compositionDirectory);
    }

    private string CreateWorkPath(
        string compositionDirectory,
        RepositoryFileKind kind,
        int fileIndex,
        int stage,
        string relativePath)
    {
        string kindDirectory = kind switch
        {
            RepositoryFileKind.Assets => "assets",
            RepositoryFileKind.Payload => "payload",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported file kind."),
        };
        string stageDirectory = Path.Combine(
            compositionDirectory,
            kindDirectory,
            fileIndex.ToString("D6"),
            $"stage-{stage:D6}");
        _fileSystemOperations.EnsureDirectory(stageDirectory);

        string outputPath = _pathResolver.ResolveWithinDirectory(stageDirectory, relativePath);
        string outputDirectory = Path.GetDirectoryName(outputPath) ??
                                 throw new IOException($"Cannot resolve composition output directory: {outputPath}");
        _fileSystemOperations.EnsureDirectory(outputDirectory);

        return outputPath;
    }

    private string ToGameRelativePath(string gameDirectory, string destinationPath)
    {
        string normalizedDestination = TrustedPath.NormalizeAbsolutePath(destinationPath);

        if (TrustedPath.PathsEqual(normalizedDestination, gameDirectory) ||
            !TrustedPath.IsWithinRoot(normalizedDestination, gameDirectory))
        {
            throw new InvalidDataException($"Payload destination is outside the game directory: {destinationPath}");
        }

        string relativePath = Path.GetRelativePath(gameDirectory, normalizedDestination);

        if (!TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedRelativePath))
        {
            throw new InvalidDataException($"Payload destination is not a trusted relative path: {destinationPath}");
        }

        string resolvedPath = _pathResolver.ResolveWithinDirectory(gameDirectory, normalizedRelativePath);

        if (!TrustedPath.PathsEqual(resolvedPath, normalizedDestination))
        {
            throw new InvalidDataException($"Payload destination could not be resolved safely: {destinationPath}");
        }

        return normalizedRelativePath;
    }

    private void EnsureRegularFile(string path, string description)
    {
        FileAttributes attributes = _fileSystemOperations.GetAttributes(path);

        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"{description} must be a regular file: {path}");
        }
    }

    private PatchPlanner CreatePatchPlanner(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);
        var fieldPatchPlanner = new FieldPatchPlanner(assetQueryService, _operationHandlers);
        var replacementPlanner = new ReplacementPlanner(assetQueryService);
        var copyAssetPlanner = new CopyAssetPlanner(assetQueryService);

        return new PatchPlanner(fieldPatchPlanner, replacementPlanner, copyAssetPlanner);
    }

    private static TResult RequirePackageResult<TResult>(OperationResult<TResult> result)
    {
        return result switch
        {
            OperationSucceeded<TResult> succeeded => succeeded.Value,
            OperationFailed<TResult> failed => throw PackageFailure(failed.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private static InvalidDataException PackageFailure(OperationError error)
    {
        return new InvalidDataException(ModOperationErrorMapper.FormatPackageFailure(error));
    }

    private sealed record FileCompositionAttempt(
        CompositionFileResult? Result,
        CompositionFailure? Failure);

    private sealed record PayloadProvider(LayerRecord Layer, string Source);
}
