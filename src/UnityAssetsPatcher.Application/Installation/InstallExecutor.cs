using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Installation;

public sealed record InstallExecutionResult(
    IReadOnlyList<InstallPatchAppliedFile> PatchedFiles,
    IReadOnlyList<InstallChange> CopiedFiles,
    string InstallId,
    int BaseSnapshotCount);

public sealed record InstallPatchAppliedFile(
    string Target,
    string AssetsFilePath,
    string? BackupPath,
    int AssetCount,
    int OperationCount);

public sealed class InstallExecutor
{
    private const string LayerPackageFileName = "package.zip";

    private readonly RepositoryService _repositoryService;
    private readonly ICompositionRepository _compositionRepository;
    private readonly BaseSnapshotCapturer _baseSnapshotCapturer;
    private readonly ModComposer _modComposer;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;
    private readonly ILogger<InstallExecutor> _logger;

    public InstallExecutor(
        RepositoryService repositoryService,
        ICompositionRepository compositionRepository,
        BaseSnapshotCapturer baseSnapshotCapturer,
        ModComposer modComposer,
        IFileSystemOperations fileSystemOperations,
        ILogger<InstallExecutor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(repositoryService);
        ArgumentNullException.ThrowIfNull(compositionRepository);
        ArgumentNullException.ThrowIfNull(baseSnapshotCapturer);
        ArgumentNullException.ThrowIfNull(modComposer);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _repositoryService = repositoryService;
        _compositionRepository = compositionRepository;
        _baseSnapshotCapturer = baseSnapshotCapturer;
        _modComposer = modComposer;
        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
        _logger = logger ?? NullLogger<InstallExecutor>.Instance;
    }

    public InstallExecutionResult Execute(
        string packagePath,
        InstallAnalysis analysis,
        RepositoryOperationLock operationLock,
        StepTimer timings,
        IReadOnlyList<PreparedInstallAssetFile>? expectedAssetFiles = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(operationLock);
        ArgumentNullException.ThrowIfNull(timings);

        RepositoryMetadata repository = _repositoryService.RequireWritableMetadata();
        string normalizedPackagePath = _fileSystemOperations.ResolveExistingFile(packagePath);
        string gameDirectory = _pathResolver.ResolveExistingDirectory(analysis.GameDirectory);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(_pathResolver, gameDirectory);
        var activeLayers = GetActiveLayers(fingerprint);
        long sequence = InstallSequenceAllocator.Allocate(activeLayers, fingerprint, repository.RepositoryId);
        string installId = Guid.NewGuid().ToString("N");
        FileIntegrity packageIntegrity = _fileSystemOperations.ComputeFileIntegrity(normalizedPackagePath);
        LayerRecord layer = CreateLayerRecord(
            repository,
            fingerprint,
            sequence,
            installId,
            analysis,
            packageIntegrity,
            gameDirectory);

        _logger.LogInformation(
            "Executing layered install {InstallId} for {ModName} {ModVersion} in {GameDirectory}",
            installId,
            analysis.Manifest.Name,
            analysis.Manifest.Version,
            gameDirectory);

        string temporaryDirectory = _repositoryService.CreateTransactionDirectory();
        string rollbackDirectory = Path.Combine(temporaryDirectory, "rollback");
        string preparedDirectory = Path.Combine(temporaryDirectory, "prepared");
        string preparedLayerDirectory = Path.Combine(temporaryDirectory, "prepared-layer");
        var transactionFiles = new List<RepositoryTransactionFile>();
        var patched = new List<InstallPatchAppliedFile>();
        IReadOnlyList<InstallChange> copied = analysis.PayloadFiles
            .Select(file => new InstallChange(InstallChangeKind.Payload, file.Source, file.DestinationPath))
            .ToArray();
        bool transactionSaved = false;
        RepositoryTransaction? transaction = null;

        try
        {
            _fileSystemOperations.EnsureDirectory(rollbackDirectory);
            _fileSystemOperations.EnsureDirectory(preparedDirectory);

            int baseSnapshotCount = CaptureBaseSnapshots(operationLock, gameDirectory, analysis, fingerprint);
            PrepareLayer(layer, normalizedPackagePath, preparedLayerDirectory);

            CompositionResult composition = Compose(
                gameDirectory,
                preparedDirectory,
                activeLayers,
                layer,
                preparedLayerDirectory,
                analysis,
                timings);
            var expectedAssetIntegrities = CreateExpectedAssetIntegrities(
                expectedAssetFiles);

            BuildTransactionFiles(
                composition,
                analysis,
                gameDirectory,
                temporaryDirectory,
                rollbackDirectory,
                expectedAssetIntegrities,
                transactionFiles,
                patched);

            transaction = new RepositoryTransaction(
                repository.RepositoryId,
                RepositoryOperationKind.Install,
                installId,
                fingerprint,
                transactionFiles);
            RepositoryTransactionStore.Save(_fileSystemOperations, temporaryDirectory, transaction);
            transactionSaved = true;

            ApplyPreparedFiles(transaction, temporaryDirectory, gameDirectory);
            _compositionRepository.Layers.CommitLayer(preparedLayerDirectory, installId);
            _fileSystemOperations.DeleteDirectory(temporaryDirectory);
            _logger.LogInformation("Committed layered install {InstallId}", installId);

            return new InstallExecutionResult(patched, copied, installId, baseSnapshotCount);
        }
        catch (Exception failure)
        {
            HandleFailure(failure, transactionSaved, transaction, temporaryDirectory, gameDirectory);

            throw;
        }
    }

    private IReadOnlyList<LayerRecord> GetActiveLayers(string fingerprint)
    {
        LayerRecord[] layers =
        [
            .. _compositionRepository.Layers
                .ListLayers()
                .Select(entry => entry.Record)
                .Where(layer => TrustedPath.PathComparer.Equals(layer.GameInstanceFingerprint, fingerprint))
                .OrderBy(layer => layer.InstallSequence)
                .ThenBy(layer => layer.Id, StringComparer.Ordinal),
        ];

        return layers;
    }

    private LayerRecord CreateLayerRecord(
        RepositoryMetadata repository,
        string fingerprint,
        long sequence,
        string installId,
        InstallAnalysis analysis,
        FileIntegrity packageIntegrity,
        string gameDirectory)
    {
        return new LayerRecord(
            repository.RepositoryId,
            fingerprint,
            sequence,
            installId,
            DateTimeOffset.Now,
            analysis.Manifest.Name,
            analysis.Manifest.Version,
            analysis.Manifest.Author,
            analysis.Manifest.Game,
            analysis.AppliedOptionalGroups.Count == 0 ? null : analysis.AppliedOptionalGroups,
            true,
            new LayerPackageInfo(LayerPackageFileName, packageIntegrity),
            analysis.Targets
                .Select(target => ToGameRelativePath(gameDirectory, target.AssetsFilePath))
                .Distinct(TrustedPath.PathComparer)
                .ToArray(),
            analysis.PayloadFiles
                .Select(file => ToGameRelativePath(gameDirectory, file.DestinationPath))
                .Distinct(TrustedPath.PathComparer)
                .ToArray());
    }

    private int CaptureBaseSnapshots(
        RepositoryOperationLock operationLock,
        string gameDirectory,
        InstallAnalysis analysis,
        string fingerprint)
    {
        BaseCatalog? existingCatalog = _compositionRepository.BaseSnapshots.TryReadCatalog(fingerprint);
        var existingAssets = existingCatalog?.AssetsFiles
            .Select(file => file.RelativePath)
            .ToHashSet(TrustedPath.PathComparer) ?? [];
        var existingPayloads = existingCatalog?.PayloadTargets
            .Select(file => file.RelativePath)
            .ToHashSet(TrustedPath.PathComparer) ?? [];
        var capturedAssets = new HashSet<string>(TrustedPath.PathComparer);
        var capturedPayloads = new HashSet<string>(TrustedPath.PathComparer);
        int capturedCount = 0;

        foreach (InstallTargetAnalysis target in analysis.Targets)
        {
            string relativePath = ToGameRelativePath(gameDirectory, target.AssetsFilePath);

            if (!capturedAssets.Add(relativePath))
            {
                continue;
            }

            if (!existingAssets.Contains(relativePath))
            {
                capturedCount++;
            }

            _baseSnapshotCapturer.Capture(operationLock, gameDirectory, relativePath, RepositoryFileKind.Assets);
        }

        foreach (InstallPayloadFilePlan payload in analysis.PayloadFiles)
        {
            string relativePath = ToGameRelativePath(gameDirectory, payload.DestinationPath);

            if (!capturedPayloads.Add(relativePath))
            {
                continue;
            }

            if (!existingPayloads.Contains(relativePath))
            {
                capturedCount++;
            }

            _baseSnapshotCapturer.Capture(operationLock, gameDirectory, relativePath, RepositoryFileKind.Payload);
        }

        return capturedCount;
    }

    private void PrepareLayer(LayerRecord layer, string packagePath, string preparedLayerDirectory)
    {
        _compositionRepository.Layers.StoreVerifiedPackage(
            packagePath,
            preparedLayerDirectory,
            layer.Package);
        _compositionRepository.Layers.WritePreparedLayer(layer, preparedLayerDirectory);
    }

    private CompositionResult Compose(
        string gameDirectory,
        string preparedDirectory,
        IReadOnlyList<LayerRecord> activeLayers,
        LayerRecord newLayer,
        string preparedLayerDirectory,
        InstallAnalysis analysis,
        StepTimer timings)
    {
        CompositionFileTarget[] files =
        [
            .. analysis.Targets.Select(target => new CompositionFileTarget(
                RepositoryFileKind.Assets,
                ToGameRelativePath(gameDirectory, target.AssetsFilePath))),
            .. analysis.PayloadFiles.Select(payload => new CompositionFileTarget(
                RepositoryFileKind.Payload,
                ToGameRelativePath(gameDirectory, payload.DestinationPath))),
        ];
        LayerRecord[] layers = [.. activeLayers, newLayer];
        string packagePath = _pathResolver.ResolveWithinDirectory(
            preparedLayerDirectory,
            newLayer.Package.FileName);
        Dictionary<string, string> layerPackagePaths = new(TrustedPath.PathComparer)
        {
            [newLayer.Id] = packagePath,
        };
        CompositionRequest request = new(
            gameDirectory,
            preparedDirectory,
            layers,
            null,
            files,
            layerPackagePaths);
        CompositionOutcome outcome = timings.Measure("compose", () => _modComposer.Compose(request));

        return outcome switch
        {
            CompositionSucceeded succeeded => succeeded.Result,
            CompositionFailed failed => throw new PatchPlanningException(failed.Failure.Diagnostics[0]),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    private void BuildTransactionFiles(
        CompositionResult composition,
        InstallAnalysis analysis,
        string gameDirectory,
        string temporaryDirectory,
        string rollbackDirectory,
        IReadOnlyDictionary<string, FileIntegrity>? expectedAssetIntegrities,
        ICollection<RepositoryTransactionFile> transactionFiles,
        ICollection<InstallPatchAppliedFile> patched)
    {
        var analysesByPath = analysis.Targets
            .ToDictionary(
                target => ToGameRelativePath(gameDirectory, target.AssetsFilePath),
                target => target,
                TrustedPath.PathComparer);

        int fileIndex = 0;

        foreach (CompositionFileResult result in composition.Files)
        {
            string targetPath = _pathResolver.ResolveWithinDirectory(gameDirectory, result.RelativePath);
            FileIntegrity? before = TryComputeFileIntegrity(targetPath);

            if (result.Kind == RepositoryFileKind.Assets)
            {
                if (expectedAssetIntegrities is not null &&
                    !expectedAssetIntegrities.ContainsKey(targetPath))
                {
                    throw new InstallPreparationStaleException(
                        $"The install preview did not contain the target assets file: {targetPath}");
                }

                if (expectedAssetIntegrities is not null &&
                    (before is null || !expectedAssetIntegrities[targetPath].Matches(before)))
                {
                    throw new InstallPreparationStaleException(
                        $"The target assets file changed after the install preview: {targetPath}");
                }
            }

            string preparedPath = result.PreparedPath ?? throw new InvalidOperationException(
                $"Layered install cannot delete target file: {result.RelativePath}");
            preparedPath = _fileSystemOperations.ResolveExistingFile(preparedPath);
            EnsureRegularFile(preparedPath, "Prepared install file");
            FileIntegrity after = _fileSystemOperations.ComputeFileIntegrity(preparedPath);
            string? rollbackRelativePath = null;

            if (before is not null)
            {
                string rollbackPath = Path.Combine(rollbackDirectory, $"file-{fileIndex:D6}.bin");
                _fileSystemOperations.CopyFileAtomically(targetPath, rollbackPath, FileDestinationMode.CreateNew);

                if (!_fileSystemOperations.MatchesFile(rollbackPath, before))
                {
                    throw new IOException($"Rollback verification failed: {targetPath}");
                }

                rollbackRelativePath = ToTransactionRelativePath(temporaryDirectory, rollbackPath);
            }

            transactionFiles.Add(new RepositoryTransactionFile(
                result.Kind,
                result.RelativePath,
                before,
                after,
                rollbackRelativePath,
                ToTransactionRelativePath(temporaryDirectory, preparedPath)));

            if (result.Kind == RepositoryFileKind.Assets)
            {
                InstallTargetAnalysis target = analysesByPath[result.RelativePath];
                (int assetCount, int operationCount) = GetPlanCounts(
                    target.PlanningResult.Plan ?? throw new InvalidOperationException(
                        "Apply analysis did not contain a patch plan."));
                patched.Add(new InstallPatchAppliedFile(
                    target.Target,
                    target.AssetsFilePath,
                    null,
                    assetCount,
                    operationCount));
            }

            fileIndex++;
        }
    }

    private static (int AssetCount, int OperationCount) GetPlanCounts(PatchPlan plan)
    {
        return plan switch
        {
            FieldPatchPlan fieldPlan => (
                fieldPlan.Assets.Count,
                fieldPlan.Assets.Sum(asset => asset.Operations.Count)),
            AssetReplacementPlan replacementPlan => (
                replacementPlan.Replacements.Count,
                replacementPlan.Replacements.Count),
            FieldPatchAndCopyPlan copyPlan => (
                copyPlan.FieldPatches
                    .Select(asset => asset.PathId)
                    .Concat(copyPlan.Copies.Select(copy => copy.TargetPathId))
                    .Distinct()
                    .Count(),
                copyPlan.FieldPatches.Sum(asset => asset.Operations.Count) + copyPlan.Copies.Count),
            _ => throw new ArgumentOutOfRangeException(nameof(plan)),
        };
    }

    private IReadOnlyDictionary<string, FileIntegrity>? CreateExpectedAssetIntegrities(
        IReadOnlyList<PreparedInstallAssetFile>? expectedAssetFiles)
    {
        if (expectedAssetFiles is null)
        {
            return null;
        }

        return expectedAssetFiles.ToDictionary(
            file => TrustedPath.NormalizeAbsolutePath(file.Path),
            file => file.Integrity,
            TrustedPath.PathComparer);
    }

    private void HandleFailure(
        Exception failure,
        bool transactionSaved,
        RepositoryTransaction? transaction,
        string temporaryDirectory,
        string gameDirectory)
    {
        if (!transactionSaved)
        {
            _logger.LogError(failure, "Install failed before the transaction was saved; temporary files removed");

            if (Directory.Exists(temporaryDirectory))
            {
                _fileSystemOperations.DeleteDirectory(temporaryDirectory);
            }

            return;
        }

        _logger.LogError(failure, "Install failed after the transaction was saved; attempting automatic rollback");
        RepositoryRecoveryReport recovery = _repositoryService.RecoverTrustedUnderLock(transaction!, gameDirectory);

        if (recovery.Status != RepositoryRecoveryStatus.Locked)
        {
            return;
        }

        _logger.LogWarning("Automatic rollback was unsafe; manual recovery is required");

        throw new RepositoryRecoveryException("Install failed and automatic rollback was unsafe.", recovery, failure);
    }

    private void ApplyPreparedFiles(RepositoryTransaction transaction, string temporaryDirectory, string gameDirectory)
    {
        foreach (RepositoryTransactionFile file in transaction.Files)
        {
            string target = _fileSystemOperations.ResolveWithinDirectory(gameDirectory, file.RelativePath);

            if (file.Before is null
                    ? File.Exists(target)
                    : !_fileSystemOperations.MatchesFile(target, file.Before))
            {
                throw new IOException($"Install target changed before mutation: {target}");
            }
        }

        foreach (RepositoryTransactionFile file in transaction.Files)
        {
            string target = _fileSystemOperations.ResolveWithinDirectory(gameDirectory, file.RelativePath);
            string source = _fileSystemOperations.ResolveWithinDirectory(
                temporaryDirectory,
                file.PreparedRelativePath ?? throw new InvalidOperationException("Prepared file path is missing."));

            _fileSystemOperations.CopyFile(source, target);

            if (file.After is null || !_fileSystemOperations.MatchesFile(target, file.After))
            {
                throw new IOException($"Installed file verification failed: {target}");
            }
        }
    }

    private FileIntegrity? TryComputeFileIntegrity(string path)
    {
        try
        {
            EnsureRegularFile(path, "Install target");

            return _fileSystemOperations.ComputeFileIntegrity(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private void EnsureRegularFile(string path, string description)
    {
        FileAttributes attributes = _fileSystemOperations.GetAttributes(path);

        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"{description} must be a regular file: {path}");
        }
    }

    private string ToGameRelativePath(string gameDirectory, string path)
    {
        string normalizedPath = TrustedPath.NormalizeAbsolutePath(path);

        if (TrustedPath.PathsEqual(normalizedPath, gameDirectory) ||
            !TrustedPath.IsWithinRoot(normalizedPath, gameDirectory))
        {
            throw new InvalidDataException($"Install target is outside the game directory: {path}");
        }

        string relativePath = Path.GetRelativePath(gameDirectory, normalizedPath);

        if (!TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedRelativePath))
        {
            throw new InvalidDataException($"Install target is not a trusted relative path: {path}");
        }

        string resolvedPath = _pathResolver.ResolveWithinDirectory(gameDirectory, normalizedRelativePath);

        if (!TrustedPath.PathsEqual(resolvedPath, normalizedPath))
        {
            throw new InvalidDataException($"Install target could not be resolved safely: {path}");
        }

        return normalizedRelativePath;
    }

    private static string ToTransactionRelativePath(string transactionDirectory, string path)
    {
        string normalizedPath = TrustedPath.NormalizeAbsolutePath(path);

        if (TrustedPath.PathsEqual(normalizedPath, transactionDirectory) ||
            !TrustedPath.IsWithinRoot(normalizedPath, transactionDirectory))
        {
            throw new InvalidOperationException("Prepared install file is outside the active transaction.");
        }

        string relativePath = Path.GetRelativePath(transactionDirectory, normalizedPath);

        return !TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedRelativePath)
            ? throw new InvalidOperationException("Prepared install file path is not trusted.")
            : normalizedRelativePath;
    }
}
