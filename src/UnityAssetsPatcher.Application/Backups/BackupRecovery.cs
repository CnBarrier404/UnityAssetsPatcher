using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.IO;

namespace UnityAssetsPatcher.Application.Backups;

internal sealed class BackupRecovery
{
    private readonly BackupRepository _repository;

    public BackupRecovery(BackupRepository repository)
    {
        _repository = repository;
    }

    public BackupRecoveryReport Recover()
    {
        try
        {
            BackupRepositoryMetadata metadata = _repository.LoadMetadata();
            _ = _repository.ListRecords();

            if (!Directory.Exists(_repository.TransactionDirectory))
            {
                return BackupRecoveryReport.Clean;
            }

            BackupTransaction transaction = BackupTransactionStore.Load(_repository.TransactionDirectory);
            if (!string.Equals(transaction.RepositoryId, metadata.RepositoryId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Transaction does not belong to this repository.");
            }

            ValidateTransaction(transaction);

            BackupRecoveryOperation operation = transaction.Kind == BackupOperationKind.Install
                ? RecoverInstall(transaction)
                : RecoverUninstall(transaction);

            return new BackupRecoveryReport(BackupRepositoryStatus.Recovered, [operation], []);
        }
        catch (Exception exception)
        {
            string path = Directory.Exists(_repository.TransactionDirectory)
                ? _repository.TransactionDirectory
                : _repository.BackupDirectory;

            return new BackupRecoveryReport(BackupRepositoryStatus.Locked, [],
                [new BackupRecoveryIssue("repository-unsafe", exception.Message, path)]);
        }
    }

    public BackupRecoveryReport Check()
    {
        try
        {
            BackupRepositoryMetadata metadata = _repository.LoadMetadata();
            _ = _repository.ListRecords();

            if (!Directory.Exists(_repository.TransactionDirectory))
            {
                return BackupRecoveryReport.Clean;
            }

            BackupTransaction transaction = BackupTransactionStore.Load(_repository.TransactionDirectory);

            if (!string.Equals(transaction.RepositoryId, metadata.RepositoryId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Transaction does not belong to this repository.");
            }

            ValidateTransaction(transaction);

            return new BackupRecoveryReport(BackupRepositoryStatus.RecoveryRequired, [], []);
        }
        catch (Exception exception)
        {
            string path = Directory.Exists(_repository.TransactionDirectory)
                ? _repository.TransactionDirectory
                : _repository.BackupDirectory;

            return new BackupRecoveryReport(BackupRepositoryStatus.Locked, [],
                [new BackupRecoveryIssue("repository-unsafe", exception.Message, path)]);
        }
    }

    private BackupRecoveryOperation RecoverInstall(BackupTransaction transaction)
    {
        string installDirectory = _repository.GetInstallDirectory(transaction.InstallId);

        if (Directory.Exists(installDirectory))
        {
            _ = _repository.ReadRecord(installDirectory);
            EnsureAfterState(transaction);
            DeleteTransaction();

            return new BackupRecoveryOperation("install", transaction.InstallId, "completed cleanup");
        }

        RollBackFiles(transaction);
        DeleteTransaction();

        return new BackupRecoveryOperation("install", transaction.InstallId, "rolled back");
    }

    private BackupRecoveryOperation RecoverUninstall(BackupTransaction transaction)
    {
        string installDirectory = _repository.GetInstallDirectory(transaction.InstallId);
        string removedDirectory = Path.Combine(_repository.TransactionDirectory, "removed-install");

        if (!Directory.Exists(installDirectory) && Directory.Exists(removedDirectory))
        {
            EnsureAfterState(transaction);
            DeleteTransaction();

            return new BackupRecoveryOperation("uninstall", transaction.InstallId, "completed cleanup");
        }

        if (!Directory.Exists(installDirectory))
        {
            throw new InvalidOperationException("Interrupted uninstall has neither an active nor committed record.");
        }

        RollBackFiles(transaction);
        DeleteTransaction();

        return new BackupRecoveryOperation("uninstall", transaction.InstallId, "rolled back");
    }

    private void RollBackFiles(BackupTransaction transaction)
    {
        foreach (BackupTransactionFile file in transaction.Files.Reverse())
        {
            RollBackFile(transaction.GameDirectory, file);
        }
    }

    private void RollBackFile(string gameDirectory, BackupTransactionFile file)
    {
        string target = BackupFileSystem.ResolveTrustedPath(gameDirectory, file.RelativePath);
        FileState state = Inspect(target, file.Before, file.After);

        if (state == FileState.Before)
        {
            return;
        }

        if (state != FileState.After)
        {
            throw new InvalidOperationException($"Transaction target has an unknown state: {target}");
        }

        if (file.Before is null)
        {
            File.Delete(target);

            return;
        }

        RestoreOriginalFile(file, target);
    }

    private void RestoreOriginalFile(BackupTransactionFile file, string target)
    {
        string rollback = BackupFileSystem.ResolveTrustedPath(_repository.TransactionDirectory,
            file.RollbackRelativePath ??
            throw new InvalidOperationException("Transaction rollback path is missing."));

        if (!file.Before!.Matches(rollback))
        {
            throw new InvalidOperationException($"Transaction rollback file is damaged: {rollback}");
        }

        BackupFileSystem.RestoreAtomically(rollback, target);

        if (!file.Before.Matches(target))
        {
            throw new InvalidOperationException($"Transaction rollback verification failed: {target}");
        }
    }

    private static void EnsureAfterState(BackupTransaction transaction)
    {
        foreach (BackupTransactionFile file in transaction.Files)
        {
            string target = BackupFileSystem.ResolveTrustedPath(transaction.GameDirectory, file.RelativePath);

            if (Inspect(target, file.Before, file.After) != FileState.After)
            {
                throw new InvalidOperationException($"Committed transaction target has an unknown state: {target}");
            }
        }
    }

    private static FileState Inspect(string path, FileIntegrity? before, FileIntegrity? after)
    {
        if (!File.Exists(path))
        {
            if (before is null)
            {
                return FileState.Before;
            }

            return after is null ? FileState.After : FileState.Unknown;
        }

        if (before is not null && before.Matches(path))
        {
            return FileState.Before;
        }

        if (after is not null && after.Matches(path))
        {
            return FileState.After;
        }

        return FileState.Unknown;
    }

    private static void ValidateTransaction(BackupTransaction transaction)
    {
        if (string.IsNullOrWhiteSpace(transaction.InstallId))
        {
            throw new InvalidOperationException("Transaction install ID is missing.");
        }

        if (!string.Equals(GameInstanceIdentity.CreateFingerprint(transaction.GameDirectory),
                transaction.GameInstanceFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Transaction game directory identity does not match.");
        }
    }

    private void DeleteTransaction() => Directory.Delete(_repository.TransactionDirectory, true);

    private enum FileState
    {
        Before,
        After,
        Unknown
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
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public static string ResolveTrustedPath(string rootDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"Invalid transaction target path: {relativePath}");
        }

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
            {
                resolved = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                           ?? throw new InvalidOperationException($"Cannot resolve trusted path: {path}");
            }
        }

        return Path.GetFullPath(resolved);
    }
}
