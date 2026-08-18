using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Repository;

internal sealed class RepositoryRecovery
{
    private readonly RepositoryService _repository;
    private readonly ICompositionRepository _compositionRepository;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IRepositoryTransactionStore _transactionStore;

    public RepositoryRecovery(
        RepositoryService repository,
        ICompositionRepository compositionRepository,
        IFileSystemOperations fileSystemOperations,
        IRepositoryTransactionStore transactionStore)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(compositionRepository);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(transactionStore);
        _repository = repository;
        _compositionRepository = compositionRepository;
        _fileSystemOperations = fileSystemOperations;
        _transactionStore = transactionStore;
    }

    public RepositoryRecoveryReport Check()
    {
        try
        {
            return LoadTransaction() is not null
                ? new RepositoryRecoveryReport(RepositoryRecoveryStatus.RecoveryRequired, [], [])
                : RepositoryRecoveryReport.Clean;
        }
        catch (Exception exception)
        {
            return LockedReport(exception);
        }
    }

    public RepositoryRecoveryPreview Preview(string gameDirectory)
    {
        RepositoryTransaction? transaction;
        try
        {
            transaction = LoadTransaction();
            if (transaction is null)
            {
                return new RepositoryRecoveryPreview(RepositoryRecoveryStatus.Clean, null, null, null, null, false, [],
                    []);
            }
        }
        catch (Exception exception)
        {
            return new RepositoryRecoveryPreview(
                RepositoryRecoveryStatus.Locked, null, null, null, null, false, [],
                [Issue(RepositoryRecoveryIssueCode.RepositoryUnsafe, exception, RecoveryIssuePath())]);
        }

        string? trustedRoot = null;
        try
        {
            trustedRoot = _fileSystemOperations.ResolveExistingDirectory(gameDirectory);
            RecoveryPlan plan = BuildPlan(transaction, trustedRoot);
            return new RepositoryRecoveryPreview(
                RepositoryRecoveryStatus.RecoveryRequired,
                trustedRoot,
                KindName(transaction.Kind),
                transaction.InstallId,
                plan.Action,
                true,
                plan.Files.Select(item => new RepositoryRecoveryFileChange(item.File.RelativePath, item.Action))
                    .ToArray(),
                []);
        }
        catch (Exception exception)
        {
            return new RepositoryRecoveryPreview(
                RepositoryRecoveryStatus.RecoveryRequired,
                trustedRoot,
                KindName(transaction.Kind),
                transaction.InstallId,
                null,
                false,
                [],
                [Issue(RepositoryRecoveryIssueCode.RecoveryUnsafe, exception, trustedRoot ?? gameDirectory)]);
        }
    }

    public RepositoryRecoveryReport Recover(string gameDirectory)
    {
        try
        {
            RepositoryTransaction? transaction = LoadTransaction();
            if (transaction is null)
            {
                return RepositoryRecoveryReport.Clean;
            }

            string trustedRoot = _fileSystemOperations.ResolveExistingDirectory(gameDirectory);
            return Apply(transaction, trustedRoot);
        }
        catch (Exception exception)
        {
            return LockedReport(exception);
        }
    }

    public RepositoryRecoveryReport RecoverTrusted(RepositoryTransaction transaction, string gameDirectory)
    {
        try
        {
            RepositoryMetadata metadata = _repository.LoadMetadata();
            ValidateTransaction(transaction, metadata.RepositoryId);
            string trustedRoot = _fileSystemOperations.ResolveExistingDirectory(gameDirectory);
            return Apply(transaction, trustedRoot);
        }
        catch (Exception exception)
        {
            return LockedReport(exception);
        }
    }

    private RepositoryRecoveryReport Apply(RepositoryTransaction transaction, string trustedRoot)
    {
        RecoveryPlan plan = BuildPlan(transaction, trustedRoot);
        if (plan.Action == RepositoryRecoveryPlanAction.RollBack)
        {
            RestoreLayerIfNeeded(transaction, plan);

            foreach (RecoveryFile file in plan.Files.Reverse())
            {
                if (file.Action == RepositoryRecoveryFileAction.NoChange)
                {
                    continue;
                }

                string target = _fileSystemOperations.ResolveWithinDirectory(trustedRoot, file.File.RelativePath);
                if (file.Action == RepositoryRecoveryFileAction.Delete)
                {
                    _fileSystemOperations.DeleteFile(target);
                }
                else
                {
                    RestoreOriginalFile(file.File, target);
                }
            }
        }

        DeleteTransaction();
        string action = plan.Action == RepositoryRecoveryPlanAction.RollBack ? "rolled back" : "completed cleanup";
        return new RepositoryRecoveryReport(RepositoryRecoveryStatus.Recovered,
            [new RepositoryRecoveryOperation(KindName(transaction.Kind), transaction.InstallId, action)], []);
    }

    private RecoveryPlan BuildPlan(RepositoryTransaction transaction, string trustedRoot)
    {
        if (!string.Equals(
                GameInstanceIdentity.CreateFingerprint(_fileSystemOperations, trustedRoot),
                transaction.GameInstanceFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The selected game directory does not match the pending transaction.");
        }

        _ = _repository.LoadMetadata();
        string removedDirectory = GetRemovedLayerDirectory();
        LayerLocation layerLocation = InspectLayerLocation(transaction.InstallId, removedDirectory);

        var states = new List<(RepositoryTransactionFile File, FileState State)>();
        foreach (RepositoryTransactionFile file in transaction.Files)
        {
            string target = _fileSystemOperations.ResolveWithinDirectory(trustedRoot, file.RelativePath);
            FileState state = Inspect(target, file.Before, file.After);
            states.Add((file, state));
        }

        bool committed = DetermineCompositionCommitState(transaction, layerLocation, states.Select(item => item.State));

        var files = new List<RecoveryFile>();
        foreach ((RepositoryTransactionFile file, FileState state) in states)
        {
            if (committed)
            {
                if (state != FileState.After)
                {
                    throw new InvalidOperationException(
                        $"Committed transaction target has an unknown state: {file.RelativePath}");
                }

                files.Add(new RecoveryFile(file, RepositoryRecoveryFileAction.NoChange));
                continue;
            }

            if (state == FileState.Before)
            {
                files.Add(new RecoveryFile(file, RepositoryRecoveryFileAction.NoChange));
            }
            else if (state != FileState.After)
            {
                throw new InvalidOperationException($"Transaction target has an unknown state: {file.RelativePath}");
            }
            else if (file.Before is null)
            {
                files.Add(new RecoveryFile(file, RepositoryRecoveryFileAction.Delete));
            }
            else
            {
                ValidateRollback(file);
                files.Add(new RecoveryFile(file, RepositoryRecoveryFileAction.Restore));
            }
        }

        RepositoryRecoveryPlanAction action = committed
            ? RepositoryRecoveryPlanAction.CompleteCleanup
            : RepositoryRecoveryPlanAction.RollBack;
        LayerRecoveryAction layerAction = transaction.Kind == RepositoryOperationKind.Uninstall &&
                                          layerLocation == LayerLocation.Removed &&
                                          action == RepositoryRecoveryPlanAction.RollBack
            ? LayerRecoveryAction.RestoreRemoved
            : LayerRecoveryAction.None;

        return new RecoveryPlan(action, files, layerAction);
    }

    private bool DetermineCompositionCommitState(
        RepositoryTransaction transaction,
        LayerLocation layerLocation,
        IEnumerable<FileState> states)
    {
        FileState[] fileStates = [.. states];
        bool allAfter = fileStates.All(state => state == FileState.After);
        bool hasBefore = fileStates.Any(state => state == FileState.Before);
        bool hasUnknown = fileStates.Any(state => state == FileState.Unknown);

        if (hasUnknown)
        {
            throw new InvalidOperationException("Transaction target has an unknown state.");
        }

        return transaction.Kind switch
        {
            RepositoryOperationKind.Install when layerLocation == LayerLocation.Active && allAfter => true,
            RepositoryOperationKind.Install when layerLocation == LayerLocation.Missing => false,
            RepositoryOperationKind.Uninstall when layerLocation == LayerLocation.Removed && allAfter => true,
            RepositoryOperationKind.Uninstall when layerLocation == LayerLocation.Active => false,
            RepositoryOperationKind.Uninstall when layerLocation == LayerLocation.Removed && hasBefore => false,
            _ => throw new InvalidOperationException("Layer state does not match the interrupted transaction.")
        };
    }

    private LayerLocation InspectLayerLocation(string installId, string removedDirectory)
    {
        string layerDirectory = _compositionRepository.Layers.GetLayerDirectory(installId);
        bool active = TryGetRealDirectory(layerDirectory, "Layer directory");
        bool removed = TryGetRealDirectory(removedDirectory, "Removed layer directory");

        if (active && removed)
        {
            throw new InvalidOperationException("The active and removed layer directories both exist.");
        }

        if (active)
        {
            _ = _compositionRepository.Layers.ReadLayer(installId);
            return LayerLocation.Active;
        }

        return removed ? LayerLocation.Removed : LayerLocation.Missing;
    }

    private void RestoreLayerIfNeeded(RepositoryTransaction transaction, RecoveryPlan plan)
    {
        if (plan.LayerAction != LayerRecoveryAction.RestoreRemoved)
        {
            return;
        }

        string removedDirectory = GetRemovedLayerDirectory();
        string layerDirectory = _compositionRepository.Layers.GetLayerDirectory(transaction.InstallId);

        if (!TryGetRealDirectory(removedDirectory, "Removed layer directory"))
        {
            if (TryGetRealDirectory(layerDirectory, "Layer directory"))
            {
                _ = _compositionRepository.Layers.ReadLayer(transaction.InstallId);

                return;
            }

            throw new InvalidOperationException("The removed layer directory is missing.");
        }

        if (TryGetRealDirectory(layerDirectory, "Layer directory"))
        {
            throw new InvalidOperationException("The active and removed layer directories both exist.");
        }

        _fileSystemOperations.EnsureDirectory(_compositionRepository.Layers.LayersDirectory);
        _fileSystemOperations.MoveDirectory(removedDirectory, layerDirectory);
        try
        {
            _ = _compositionRepository.Layers.ReadLayer(transaction.InstallId);
        }
        catch
        {
            _fileSystemOperations.MoveDirectory(layerDirectory, removedDirectory);

            throw;
        }
    }

    private string GetRemovedLayerDirectory()
    {
        return _fileSystemOperations.ResolveWithinDirectory(_repository.TransactionDirectory, "removed-install");
    }

    private bool TryGetRealDirectory(string path, string description)
    {
        try
        {
            FileAttributes attributes = _fileSystemOperations.GetAttributes(path);
            if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException($"{description} is not trusted: {path}");
            }

            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private RepositoryTransaction? LoadTransaction()
    {
        RepositoryMetadata metadata = _repository.LoadMetadata();
        RepositoryTransaction? transaction = _transactionStore.TryLoad();
        if (transaction is null)
        {
            return null;
        }

        ValidateTransaction(transaction, metadata.RepositoryId);
        return transaction;
    }

    private static void ValidateTransaction(RepositoryTransaction transaction, string repositoryId)
    {
        if (!string.Equals(transaction.RepositoryId, repositoryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Transaction does not belong to this repository.");
        }

        if (string.IsNullOrWhiteSpace(transaction.InstallId))
        {
            throw new InvalidOperationException("Transaction install ID is missing.");
        }

        if (string.IsNullOrWhiteSpace(transaction.GameInstanceFingerprint) ||
            transaction.GameInstanceFingerprint.Length != 64 ||
            transaction.GameInstanceFingerprint.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException("Transaction game fingerprint is invalid.");
        }

        var targets = new HashSet<string>(PathComparer);
        var rollbacks = new HashSet<string>(PathComparer);
        var prepared = new HashSet<string>(PathComparer);
        foreach (RepositoryTransactionFile file in transaction.Files)
        {
            ValidateRelativePath(file.RelativePath, "target");
            if (!targets.Add(Normalize(file.RelativePath)))
            {
                throw new InvalidOperationException("Transaction contains duplicate target paths.");
            }

            ValidateIntegrity(file.Before);
            ValidateIntegrity(file.After);

            bool valid = transaction.Kind switch
            {
                RepositoryOperationKind.Install =>
                    file.After is not null &&
                    file.PreparedRelativePath is not null &&
                    (file.Before is null
                        ? file.RollbackRelativePath is null
                        : file.RollbackRelativePath is not null),
                RepositoryOperationKind.Uninstall =>
                    (file.Before is null
                        ? file.RollbackRelativePath is null
                        : file.RollbackRelativePath is not null) &&
                    (file.After is null
                        ? file.PreparedRelativePath is null
                        : true),
                _ => false
            };
            if (!valid)
            {
                throw new InvalidOperationException($"Transaction file shape is invalid: {file.RelativePath}");
            }

            if (file.RollbackRelativePath is not null)
            {
                ValidateRelativePath(file.RollbackRelativePath, "rollback");
                if (!rollbacks.Add(Normalize(file.RollbackRelativePath)))
                {
                    throw new InvalidOperationException("Transaction contains duplicate rollback paths.");
                }
            }

            if (file.PreparedRelativePath is not null)
            {
                ValidateRelativePath(file.PreparedRelativePath, "prepared");

                if (!prepared.Add(Normalize(file.PreparedRelativePath)))
                {
                    throw new InvalidOperationException("Transaction contains duplicate prepared paths.");
                }
            }
        }
    }

    private void ValidateRollback(RepositoryTransactionFile file)
    {
        string rollback = _fileSystemOperations.ResolveWithinDirectory(_repository.TransactionDirectory,
            file.RollbackRelativePath ?? throw new InvalidOperationException("Transaction rollback path is missing."));
        if (!_fileSystemOperations.MatchesFile(rollback, file.Before!))
        {
            throw new InvalidOperationException($"Transaction rollback file is damaged: {rollback}");
        }
    }

    private void RestoreOriginalFile(RepositoryTransactionFile file, string target)
    {
        string rollback = _fileSystemOperations.ResolveWithinDirectory(_repository.TransactionDirectory,
            file.RollbackRelativePath ?? throw new InvalidOperationException("Transaction rollback path is missing."));
        if (!_fileSystemOperations.MatchesFile(rollback, file.Before!))
        {
            throw new InvalidOperationException($"Transaction rollback file is damaged: {rollback}");
        }

        _fileSystemOperations.CopyFile(rollback, target);
        if (!_fileSystemOperations.MatchesFile(target, file.Before!))
        {
            throw new InvalidOperationException($"Transaction rollback verification failed: {target}");
        }
    }

    private void DeleteTransaction()
    {
        _transactionStore.Delete();
    }

    private FileState Inspect(string path, FileIntegrity? before, FileIntegrity? after)
    {
        if (!File.Exists(path))
        {
            if (before is null)
            {
                return FileState.Before;
            }

            return after is null ? FileState.After : FileState.Unknown;
        }

        if (before is not null && _fileSystemOperations.MatchesFile(path, before))
        {
            return FileState.Before;
        }

        if (after is not null && _fileSystemOperations.MatchesFile(path, after))
        {
            return FileState.After;
        }

        return FileState.Unknown;
    }

    private static void ValidateIntegrity(FileIntegrity? integrity)
    {
        if (integrity is null)
        {
            return;
        }

        if (integrity.Length < 0 || integrity.Sha256.Length != 64 ||
            integrity.Sha256.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException("Transaction file integrity is invalid.");
        }
    }

    private static void ValidateRelativePath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"Transaction {description} path is not trusted: {path}");
        }
    }

    private RepositoryRecoveryReport LockedReport(Exception exception)
    {
        return new RepositoryRecoveryReport(RepositoryRecoveryStatus.Locked, [],
            [Issue(RepositoryRecoveryIssueCode.RepositoryUnsafe, exception, RecoveryIssuePath())]);
    }

    private string RecoveryIssuePath()
    {
        return Directory.Exists(_repository.TransactionDirectory)
            ? _repository.TransactionDirectory
            : _repository.RepositoryDirectory;
    }

    private static RepositoryRecoveryIssue Issue(
        RepositoryRecoveryIssueCode code,
        Exception exception,
        string path)
    {
        return new RepositoryRecoveryIssue(code, path)
        {
            Parameters = new Dictionary<string, string> { ["detail"] = exception.Message }
        };
    }

    private static string KindName(RepositoryOperationKind kind)
    {
        return kind == RepositoryOperationKind.Install ? "install" : "uninstall";
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record RecoveryPlan(
        RepositoryRecoveryPlanAction Action,
        IReadOnlyList<RecoveryFile> Files,
        LayerRecoveryAction LayerAction);

    private sealed record RecoveryFile(RepositoryTransactionFile File, RepositoryRecoveryFileAction Action);

    private enum FileState
    {
        Before,
        After,
        Unknown
    }

    private enum LayerLocation
    {
        Missing,
        Active,
        Removed
    }

    private enum LayerRecoveryAction
    {
        None,
        RestoreRemoved
    }
}
