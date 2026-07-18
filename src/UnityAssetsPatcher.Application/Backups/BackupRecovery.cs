using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Application.Backups;

internal sealed class BackupRecovery
{
    private readonly BackupRepository _repository;

    public BackupRecovery(BackupRepository repository)
    {
        _repository = repository;
    }

    public BackupRecoveryReport Check()
    {
        try
        {
            _ = LoadTransaction();
            return Directory.Exists(_repository.TransactionDirectory)
                ? new BackupRecoveryReport(BackupRepositoryStatus.RecoveryRequired, [], [])
                : BackupRecoveryReport.Clean;
        }
        catch (Exception exception)
        {
            return LockedReport(exception);
        }
    }

    public BackupRecoveryPreview Preview(string gameDirectory)
    {
        BackupTransaction? transaction;
        try
        {
            transaction = LoadTransaction();
            if (transaction is null)
            {
                return new BackupRecoveryPreview(BackupRepositoryStatus.Clean, null, null, null, null, false, [], []);
            }
        }
        catch (Exception exception)
        {
            return new BackupRecoveryPreview(
                BackupRepositoryStatus.Locked, null, null, null, null, false, [],
                [new BackupRecoveryIssue("repository-unsafe", exception.Message, RecoveryIssuePath())]);
        }

        string? trustedRoot = null;
        try
        {
            trustedRoot = GameInstanceIdentity.ResolveDirectory(gameDirectory);
            RecoveryPlan plan = BuildPlan(transaction, trustedRoot);
            return new BackupRecoveryPreview(
                BackupRepositoryStatus.RecoveryRequired,
                trustedRoot,
                KindName(transaction.Kind),
                transaction.InstallId,
                plan.Action,
                true,
                plan.Files.Select(item => new BackupRecoveryFileChange(item.File.RelativePath, item.Action)).ToArray(),
                []);
        }
        catch (Exception exception)
        {
            return new BackupRecoveryPreview(
                BackupRepositoryStatus.RecoveryRequired,
                trustedRoot,
                KindName(transaction.Kind),
                transaction.InstallId,
                null,
                false,
                [],
                [new BackupRecoveryIssue("recovery-unsafe", exception.Message, trustedRoot ?? gameDirectory)]);
        }
    }

    public BackupRecoveryReport Recover(string gameDirectory)
    {
        try
        {
            BackupTransaction? transaction = LoadTransaction();
            if (transaction is null) return BackupRecoveryReport.Clean;

            string trustedRoot = GameInstanceIdentity.ResolveDirectory(gameDirectory);
            return Apply(transaction, trustedRoot);
        }
        catch (Exception exception)
        {
            return LockedReport(exception);
        }
    }

    public BackupRecoveryReport RecoverTrusted(BackupTransaction transaction, string gameDirectory)
    {
        try
        {
            BackupRepositoryMetadata metadata = _repository.LoadMetadata();
            _ = _repository.ListRecords();
            ValidateTransaction(transaction, metadata.RepositoryId);
            string trustedRoot = GameInstanceIdentity.ResolveDirectory(gameDirectory);
            return Apply(transaction, trustedRoot);
        }
        catch (Exception exception)
        {
            return LockedReport(exception);
        }
    }

    private BackupRecoveryReport Apply(BackupTransaction transaction, string trustedRoot)
    {
        RecoveryPlan plan = BuildPlan(transaction, trustedRoot);
        if (plan.Action == BackupRecoveryPlanAction.RollBack)
        {
            foreach (RecoveryFile file in plan.Files.Reverse())
            {
                if (file.Action == BackupRecoveryFileAction.NoChange) continue;
                string target = BackupFileSystem.ResolveTrustedPath(trustedRoot, file.File.RelativePath);
                if (file.Action == BackupRecoveryFileAction.Delete)
                {
                    File.Delete(target);
                }
                else
                {
                    RestoreOriginalFile(file.File, target);
                }
            }
        }

        DeleteTransaction();
        string action = plan.Action == BackupRecoveryPlanAction.RollBack ? "rolled back" : "completed cleanup";
        return new BackupRecoveryReport(BackupRepositoryStatus.Recovered,
            [new BackupRecoveryOperation(KindName(transaction.Kind), transaction.InstallId, action)], []);
    }

    private RecoveryPlan BuildPlan(BackupTransaction transaction, string trustedRoot)
    {
        if (!string.Equals(GameInstanceIdentity.CreateFingerprint(trustedRoot), transaction.GameInstanceFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The selected game directory does not match the pending transaction.");
        }

        string installDirectory = _repository.GetInstallDirectory(transaction.InstallId);
        string removedDirectory = BackupFileSystem.ResolveTrustedPath(_repository.TransactionDirectory,
            "removed-install");
        bool committed = transaction.Kind == BackupOperationKind.Install
            ? Directory.Exists(installDirectory)
            : !Directory.Exists(installDirectory) && Directory.Exists(removedDirectory);

        if (transaction.Kind == BackupOperationKind.Uninstall &&
            !Directory.Exists(installDirectory) && !Directory.Exists(removedDirectory))
        {
            throw new InvalidOperationException("Interrupted uninstall has neither an active nor committed record.");
        }

        if (committed && transaction.Kind == BackupOperationKind.Install)
        {
            InstallRecord record = _repository.ReadRecord(installDirectory);
            if (!string.Equals(record.Id, transaction.InstallId, StringComparison.Ordinal) ||
                !string.Equals(record.GameInstanceFingerprint, transaction.GameInstanceFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Committed install record does not match the transaction.");
            }
        }

        var files = new List<RecoveryFile>();
        foreach (BackupTransactionFile file in transaction.Files)
        {
            string target = BackupFileSystem.ResolveTrustedPath(trustedRoot, file.RelativePath);
            FileState state = Inspect(target, file.Before, file.After);
            if (committed)
            {
                if (state != FileState.After)
                    throw new InvalidOperationException($"Committed transaction target has an unknown state: {target}");
                files.Add(new RecoveryFile(file, BackupRecoveryFileAction.NoChange));
                continue;
            }

            if (state == FileState.Before)
            {
                files.Add(new RecoveryFile(file, BackupRecoveryFileAction.NoChange));
            }
            else if (state != FileState.After)
            {
                throw new InvalidOperationException($"Transaction target has an unknown state: {target}");
            }
            else if (file.Before is null)
            {
                files.Add(new RecoveryFile(file, BackupRecoveryFileAction.Delete));
            }
            else
            {
                ValidateRollback(file);
                files.Add(new RecoveryFile(file, BackupRecoveryFileAction.Restore));
            }
        }

        return new RecoveryPlan(
            committed ? BackupRecoveryPlanAction.CompleteCleanup : BackupRecoveryPlanAction.RollBack,
            files);
    }

    private BackupTransaction? LoadTransaction()
    {
        BackupRepositoryMetadata metadata = _repository.LoadMetadata();
        _ = _repository.ListRecords();
        if (!Directory.Exists(_repository.TransactionDirectory)) return null;

        EnsureRealDirectory(_repository.TransactionDirectory, "Transaction directory");
        BackupTransaction transaction = BackupTransactionStore.Load(_repository.TransactionDirectory);
        ValidateTransaction(transaction, metadata.RepositoryId);
        return transaction;
    }

    private static void ValidateTransaction(BackupTransaction transaction, string repositoryId)
    {
        if (!string.Equals(transaction.RepositoryId, repositoryId, StringComparison.Ordinal))
            throw new InvalidOperationException("Transaction does not belong to this repository.");
        if (string.IsNullOrWhiteSpace(transaction.InstallId))
            throw new InvalidOperationException("Transaction install ID is missing.");
        if (string.IsNullOrWhiteSpace(transaction.GameInstanceFingerprint) ||
            transaction.GameInstanceFingerprint.Length != 64 ||
            transaction.GameInstanceFingerprint.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidOperationException("Transaction game fingerprint is invalid.");

        var targets = new HashSet<string>(PathComparer);
        var rollbacks = new HashSet<string>(PathComparer);
        foreach (BackupTransactionFile file in transaction.Files)
        {
            ValidateRelativePath(file.RelativePath, "target");
            if (!targets.Add(Normalize(file.RelativePath)))
                throw new InvalidOperationException("Transaction contains duplicate target paths.");
            ValidateIntegrity(file.Before);
            ValidateIntegrity(file.After);

            bool valid = transaction.Kind switch
            {
                BackupOperationKind.Install when file.Kind == BackupFileKind.Assets =>
                    file.Before is not null && file.After is not null && file.RollbackRelativePath is not null,
                BackupOperationKind.Install =>
                    file.Before is null && file.After is not null && file.RollbackRelativePath is null,
                BackupOperationKind.Uninstall when file.Kind == BackupFileKind.Assets =>
                    file.Before is not null && file.After is not null && file.RollbackRelativePath is not null,
                BackupOperationKind.Uninstall =>
                    file.Before is not null && file.After is null && file.RollbackRelativePath is not null,
                _ => false,
            };
            if (!valid) throw new InvalidOperationException($"Transaction file shape is invalid: {file.RelativePath}");

            if (file.RollbackRelativePath is not null)
            {
                ValidateRelativePath(file.RollbackRelativePath, "rollback");
                if (!rollbacks.Add(Normalize(file.RollbackRelativePath)))
                    throw new InvalidOperationException("Transaction contains duplicate rollback paths.");
            }
        }
    }

    private void ValidateRollback(BackupTransactionFile file)
    {
        string rollback = BackupFileSystem.ResolveTrustedPath(_repository.TransactionDirectory,
            file.RollbackRelativePath ?? throw new InvalidOperationException("Transaction rollback path is missing."));
        if (!file.Before!.Matches(rollback))
            throw new InvalidOperationException($"Transaction rollback file is damaged: {rollback}");
    }

    private void RestoreOriginalFile(BackupTransactionFile file, string target)
    {
        string rollback = BackupFileSystem.ResolveTrustedPath(_repository.TransactionDirectory,
            file.RollbackRelativePath ?? throw new InvalidOperationException("Transaction rollback path is missing."));
        if (!file.Before!.Matches(rollback))
            throw new InvalidOperationException($"Transaction rollback file is damaged: {rollback}");
        BackupFileSystem.RestoreAtomically(rollback, target);
        if (!file.Before.Matches(target))
            throw new InvalidOperationException($"Transaction rollback verification failed: {target}");
    }

    private void DeleteTransaction()
    {
        EnsureRealDirectory(_repository.TransactionDirectory, "Transaction directory");
        Directory.Delete(_repository.TransactionDirectory, true);
    }

    private static void EnsureRealDirectory(string path, string description)
    {
        var info = new DirectoryInfo(path);
        if (!info.Exists || info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException($"{description} is not trusted: {path}");
    }

    private static FileState Inspect(string path, FileIntegrity? before, FileIntegrity? after)
    {
        if (!File.Exists(path))
        {
            if (before is null) return FileState.Before;
            return after is null ? FileState.After : FileState.Unknown;
        }

        if (before is not null && before.Matches(path)) return FileState.Before;
        if (after is not null && after.Matches(path)) return FileState.After;
        return FileState.Unknown;
    }

    private static void ValidateIntegrity(FileIntegrity? integrity)
    {
        if (integrity is null) return;
        if (integrity.Length < 0 || integrity.Sha256.Length != 64 ||
            integrity.Sha256.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidOperationException("Transaction file integrity is invalid.");
    }

    private static void ValidateRelativePath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            throw new InvalidOperationException($"Transaction {description} path is not trusted: {path}");
    }

    private BackupRecoveryReport LockedReport(Exception exception) =>
        new(BackupRepositoryStatus.Locked, [],
            [new BackupRecoveryIssue("repository-unsafe", exception.Message, RecoveryIssuePath())]);

    private string RecoveryIssuePath() => Directory.Exists(_repository.TransactionDirectory)
        ? _repository.TransactionDirectory
        : _repository.BackupDirectory;

    private static string KindName(BackupOperationKind kind) =>
        kind == BackupOperationKind.Install ? "install" : "uninstall";

    private static string Normalize(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record RecoveryPlan(BackupRecoveryPlanAction Action, IReadOnlyList<RecoveryFile> Files);

    private sealed record RecoveryFile(BackupTransactionFile File, BackupRecoveryFileAction Action);

    private enum FileState
    {
        Before,
        After,
        Unknown,
    }
}

public static class BackupFileSystem
{
    public static void RestoreAtomically(string sourcePath, string targetPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(targetPath)) ??
                           throw new InvalidOperationException($"Cannot resolve target directory: {targetPath}");
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourcePath, tempPath, false);
            FileHelper.SafeMoveFile(tempPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public static string ResolveTrustedPath(string rootDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            throw new InvalidOperationException($"Invalid transaction target path: {relativePath}");

        string root = ResolveExistingLinks(rootDirectory);
        string path = ResolveExistingLinks(Path.Combine(root, relativePath));
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return !path.StartsWith(prefix, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal)
            ? throw new InvalidOperationException($"Transaction target escapes the trusted directory: {relativePath}")
            : path;
    }

    private static string ResolveExistingLinks(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)
                      ?? throw new InvalidOperationException($"Cannot resolve trusted path: {path}");
        string resolved = root;
        foreach (string segment in fullPath[root.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, segment);
            FileSystemInfo? info = Directory.Exists(resolved)
                ? new DirectoryInfo(resolved)
                : File.Exists(resolved)
                    ? new FileInfo(resolved)
                    : null;
            if (info?.LinkTarget is not null)
                resolved = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                           ?? throw new InvalidOperationException($"Cannot resolve trusted path: {path}");
        }

        return Path.GetFullPath(resolved);
    }
}
