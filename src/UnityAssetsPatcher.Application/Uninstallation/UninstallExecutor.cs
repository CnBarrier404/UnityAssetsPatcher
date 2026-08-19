using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed class UninstallExecutor
{
    private readonly RepositoryService _repositoryService;
    private readonly UninstallCompositionService _compositionService;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IRepositoryStore _repositoryStore;
    private readonly TrustedPathResolver _pathResolver;
    private readonly ILogger<UninstallExecutor> _logger;

    public UninstallExecutor(
        RepositoryService repositoryService,
        UninstallCompositionService compositionService,
        IFileSystemOperations fileSystemOperations,
        IRepositoryStore repositoryStore,
        ILogger<UninstallExecutor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(repositoryService);
        ArgumentNullException.ThrowIfNull(compositionService);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(repositoryStore);

        _repositoryService = repositoryService;
        _compositionService = compositionService;
        _fileSystemOperations = fileSystemOperations;
        _repositoryStore = repositoryStore;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
        _logger = logger ?? NullLogger<UninstallExecutor>.Instance;
    }

    public async Task<UninstallModResult> ExecuteAsync(
        UninstallPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        string gameDirectory = _pathResolver.ResolveExistingDirectory(plan.GameDirectory);
        string layerDirectory = _pathResolver.ResolveExistingDirectory(plan.LayerDirectory);

        _logger.LogInformation(
            "Executing layered uninstall of install {InstallId} ({ModName} {ModVersion})",
            plan.Layer.Id,
            plan.Layer.ModName,
            plan.Layer.ModVersion);

        RepositoryMetadata repository = _repositoryService.RequireWritableMetadata();
        string temporaryDirectory = _repositoryService.CreateTransactionDirectory();
        string rollbackDirectory = Path.Combine(temporaryDirectory, "rollback");
        _fileSystemOperations.EnsureDirectory(rollbackDirectory);
        var transactionFiles = new List<RepositoryTransactionFile>();
        bool transactionSaved = false;
        RepositoryTransaction? transaction = null;

        try
        {
            UninstallCompositionAnalysis analysis = await _compositionService.AnalyzeAsync(
                plan.Layer,
                gameDirectory,
                temporaryDirectory,
                cancellationToken).ConfigureAwait(false);
            BuildTransactionFiles(
                analysis,
                temporaryDirectory,
                rollbackDirectory,
                transactionFiles);

            transaction = new RepositoryTransaction(
                repository.RepositoryId,
                RepositoryOperationKind.Uninstall,
                plan.Layer.Id,
                plan.Layer.GameInstanceFingerprint,
                transactionFiles);
            _repositoryStore.Transactions.Save(transaction);
            transactionSaved = true;

            ApplyPreparedFiles(transaction, temporaryDirectory, gameDirectory);

            string removedLayerDirectory = Path.Combine(temporaryDirectory, "removed-install");
            _fileSystemOperations.MoveDirectory(layerDirectory, removedLayerDirectory);
            _fileSystemOperations.DeleteDirectory(temporaryDirectory);

            return new UninstallModResult(
                plan.Layer.Id,
                plan.Layer.ModName,
                plan.Layer.ModVersion,
                CreateResultFiles(analysis));
        }
        catch (Exception failure)
        {
            HandleFailure(failure, transactionSaved, transaction, temporaryDirectory, gameDirectory);

            throw;
        }
    }

    private void BuildTransactionFiles(
        UninstallCompositionAnalysis analysis,
        string temporaryDirectory,
        string rollbackDirectory,
        ICollection<RepositoryTransactionFile> transactionFiles)
    {
        var seenPaths = new HashSet<string>(TrustedPath.PathComparer);
        int fileIndex = 0;

        foreach (CompositionFileTarget target in analysis.Files)
        {
            if (!seenPaths.Add(target.RelativePath))
            {
                throw new InvalidDataException($"Composition contains duplicate game file: {target.RelativePath}");
            }

            CompositionFileResult current = FindCompositionFile(analysis.Current, target);
            CompositionFileResult withoutTarget = FindCompositionFile(analysis.WithoutTarget, target);
            string targetPath = _pathResolver.ResolveWithinDirectory(analysis.GameDirectory, target.RelativePath);
            FileIntegrity? expectedBefore = GetPreparedIntegrity(current);
            FileIntegrity? before = TryComputeFileIntegrity(targetPath);

            EnsureCurrentState(targetPath, expectedBefore, before);

            FileIntegrity? after = GetPreparedIntegrity(withoutTarget);

            if ((before is null && after is null) ||
                (before is not null && after is not null && before.Matches(after)))
            {
                continue;
            }

            string? rollbackRelativePath = null;
            if (before is not null)
            {
                string rollbackPath = Path.Combine(rollbackDirectory, $"file-{fileIndex:D6}.bin");
                _fileSystemOperations.CopyFileAtomically(targetPath, rollbackPath, FileDestinationMode.CreateNew);

                if (!_fileSystemOperations.MatchesFile(rollbackPath, before))
                {
                    throw new IOException($"Uninstall rollback snapshot verification failed: {targetPath}");
                }

                rollbackRelativePath = ToTransactionRelativePath(temporaryDirectory, rollbackPath);
            }

            string? preparedRelativePath = null;
            if (withoutTarget.PreparedPath is not null)
            {
                EnsureRegularFile(withoutTarget.PreparedPath, "Prepared uninstall file");
                preparedRelativePath = ToTransactionRelativePath(temporaryDirectory, withoutTarget.PreparedPath);
            }

            transactionFiles.Add(new RepositoryTransactionFile(
                target.Kind,
                target.RelativePath,
                before,
                after,
                rollbackRelativePath,
                preparedRelativePath));
            fileIndex++;
        }
    }

    private IReadOnlyList<UninstallChangedFileResult> CreateResultFiles(
        UninstallCompositionAnalysis analysis)
    {
        var results = new List<UninstallChangedFileResult>(analysis.Files.Count);

        foreach (CompositionFileTarget target in analysis.Files)
        {
            CompositionFileResult withoutTarget = FindCompositionFile(analysis.WithoutTarget, target);
            UninstallChangedFileAction action = DetermineAction(analysis, target, withoutTarget);
            FileIntegrityStatus status = withoutTarget.PreparedPath is null
                ? FileIntegrityStatus.Missing
                : FileIntegrityStatus.Matches;
            results.Add(new UninstallChangedFileResult(target.RelativePath, action, status));
        }

        return results;
    }

    private void ApplyPreparedFiles(
        RepositoryTransaction transaction,
        string temporaryDirectory,
        string gameDirectory)
    {
        foreach (RepositoryTransactionFile file in transaction.Files)
        {
            string target = _pathResolver.ResolveWithinDirectory(gameDirectory, file.RelativePath);
            FileIntegrity? before = TryComputeFileIntegrity(target);

            if (!MatchesIntegrity(before, file.Before))
            {
                throw new IOException($"Uninstall target changed before mutation: {target}");
            }
        }

        foreach (RepositoryTransactionFile file in transaction.Files)
        {
            string target = _pathResolver.ResolveWithinDirectory(gameDirectory, file.RelativePath);

            if (file.After is null)
            {
                if (TryGetAttributes(target, out _))
                {
                    _fileSystemOperations.DeleteFile(target);
                }

                if (TryGetAttributes(target, out _))
                {
                    throw new IOException($"Deleted file is still present: {target}");
                }

                continue;
            }

            string source = _pathResolver.ResolveWithinDirectory(
                temporaryDirectory,
                file.PreparedRelativePath ?? throw new InvalidOperationException(
                    "Prepared uninstall file path is missing."));
            _fileSystemOperations.CopyFile(source, target);

            if (!_fileSystemOperations.MatchesFile(target, file.After))
            {
                throw new IOException($"Uninstalled file verification failed: {target}");
            }
        }
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
            _logger.LogError(
                failure,
                "Uninstall failed before the transaction was saved; temporary files removed");

            if (Directory.Exists(temporaryDirectory))
            {
                _fileSystemOperations.DeleteDirectory(temporaryDirectory);
            }

            return;
        }

        _logger.LogError(
            failure,
            "Uninstall failed after the transaction was saved; attempting automatic rollback");
        RepositoryRecoveryReport recovery = _repositoryService.RecoverTrustedUnderLock(transaction!, gameDirectory);

        if (recovery.Status != RepositoryRecoveryStatus.Locked)
        {
            return;
        }

        _logger.LogWarning("Automatic rollback was unsafe; manual recovery is required");

        throw new RepositoryRecoveryException(
            "Uninstall failed and automatic rollback was unsafe.",
            recovery,
            failure);
    }

    private FileIntegrity? GetPreparedIntegrity(CompositionFileResult result)
    {
        return result.PreparedPath is null
            ? null
            : _fileSystemOperations.ComputeFileIntegrity(result.PreparedPath);
    }

    private void EnsureCurrentState(
        string targetPath,
        FileIntegrity? expected,
        FileIntegrity? actual)
    {
        if (!MatchesIntegrity(actual, expected))
        {
            throw new InvalidOperationException(
                $"Cannot uninstall because the current game file differs from the composed active layers: {targetPath}");
        }
    }

    private FileIntegrity? TryComputeFileIntegrity(string path)
    {
        if (!TryGetAttributes(path, out FileAttributes attributes))
        {
            return null;
        }

        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"Uninstall target must be a regular file: {path}");
        }

        return _fileSystemOperations.ComputeFileIntegrity(path);
    }

    private bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = _fileSystemOperations.GetAttributes(path);

            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;

            return false;
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

    private static bool MatchesIntegrity(FileIntegrity? actual, FileIntegrity? expected)
    {
        return actual is null
            ? expected is null
            : expected is not null && expected.Matches(actual);
    }

    private static CompositionFileResult FindCompositionFile(
        CompositionResult composition,
        CompositionFileTarget target)
    {
        return composition.Files.FirstOrDefault(file =>
                   file.Kind == target.Kind &&
                   TrustedPath.PathComparer.Equals(file.RelativePath, target.RelativePath)) ??
               throw new InvalidDataException($"Composition result is missing file: {target.RelativePath}");
    }

    private static UninstallChangedFileAction DetermineAction(
        UninstallCompositionAnalysis analysis,
        CompositionFileTarget target,
        CompositionFileResult withoutTarget)
    {
        if (withoutTarget.PreparedPath is null)
        {
            return UninstallChangedFileAction.Delete;
        }

        bool remainingLayerTouchesFile = analysis.ActiveLayers
            .Where(layer => layer.Enabled && !TrustedPath.PathComparer.Equals(layer.Id, analysis.TargetLayer.Id))
            .Any(layer => target.Kind == RepositoryFileKind.Assets
                ? layer.AssetsTargets.Contains(target.RelativePath, TrustedPath.PathComparer)
                : layer.PayloadTargets.Contains(target.RelativePath, TrustedPath.PathComparer));

        return remainingLayerTouchesFile
            ? UninstallChangedFileAction.Rebuild
            : UninstallChangedFileAction.RestoreBase;
    }

    private static string ToTransactionRelativePath(string transactionDirectory, string path)
    {
        string normalizedPath = TrustedPath.NormalizeAbsolutePath(path);

        if (TrustedPath.PathsEqual(normalizedPath, transactionDirectory) ||
            !TrustedPath.IsWithinRoot(normalizedPath, transactionDirectory))
        {
            throw new InvalidOperationException("Prepared uninstall file is outside the active transaction.");
        }

        string relativePath = Path.GetRelativePath(transactionDirectory, normalizedPath);

        return !TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedRelativePath)
            ? throw new InvalidOperationException("Prepared uninstall file path is not trusted.")
            : normalizedRelativePath;
    }
}
