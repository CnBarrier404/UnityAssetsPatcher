using System.Runtime.ExceptionServices;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Uninstallation;

public sealed class UninstallExecutor
{
    public UninstallExecutionResult Execute(UninstallPlan plan)
    {
        ValidateUninstallAccess(plan.Record, plan.InstallDirectory);

        var restoredFiles = RestoreAssets(plan.Record.PatchedFiles);
        var deletedFiles = DeleteCopiedFiles(plan.Record.CopiedFiles);

        ModBackupStore.DeleteRecord(plan.InstallDirectory);
        Directory.Delete(plan.InstallDirectory, true);

        return new UninstallExecutionResult(restoredFiles, deletedFiles);
    }

    private static void ValidateUninstallAccess(InstallRecord record, string installDirectory)
    {
        ValidateRestorableFiles(record.PatchedFiles);

        foreach (InstallRecordPatchedFile file in record.PatchedFiles)
        {
            EnsureCanOpen(file.AssetsFilePath, FileAccess.ReadWrite, FileShare.None);
            EnsureCanOpen(file.BackupPath, FileAccess.Read, FileShare.Read);
        }

        foreach (InstallRecordCopiedFile file in record.CopiedFiles)
        {
            if (File.Exists(file.DestinationPath))
            {
                EnsureCanOpen(file.DestinationPath, FileAccess.ReadWrite, FileShare.None);
            }
        }

        EnsureCanOpen(Path.Combine(installDirectory, "record.json"), FileAccess.ReadWrite, FileShare.None);
    }

    private static void EnsureCanOpen(string path, FileAccess access, FileShare share)
    {
        using FileStream _ = File.Open(path, FileMode.Open, access, share);
    }

    private static List<UninstallRestoredFileResult> RestoreAssets(IReadOnlyList<InstallRecordPatchedFile> files)
    {
        ValidateRestorableFiles(files);

        return RestorePatchedFiles(files);
    }

    private static List<UninstallDeletedFileResult> DeleteCopiedFiles(IReadOnlyList<InstallRecordCopiedFile> files)
    {
        var deletedFiles = new List<UninstallDeletedFileResult>();

        foreach (InstallRecordCopiedFile file in files)
        {
            if (!File.Exists(file.DestinationPath))
            {
                deletedFiles.Add(new UninstallDeletedFileResult(file.Source, file.DestinationPath, false));
                continue;
            }

            File.Delete(file.DestinationPath);
            deletedFiles.Add(new UninstallDeletedFileResult(file.Source, file.DestinationPath, true));
        }

        return deletedFiles;
    }

    private static void ValidateRestorableFiles(IReadOnlyList<InstallRecordPatchedFile> files)
    {
        foreach (InstallRecordPatchedFile file in files)
        {
            if (!File.Exists(file.AssetsFilePath))
            {
                throw new FileNotFoundException(
                    $"Assets file was deleted during uninstall: {file.AssetsFilePath}",
                    file.AssetsFilePath);
            }

            if (!File.Exists(file.BackupPath))
            {
                throw new FileNotFoundException(
                    $"Backup file was deleted during uninstall: {file.BackupPath}",
                    file.BackupPath);
            }
        }
    }

    private static List<UninstallRestoredFileResult> RestorePatchedFiles(
        IReadOnlyList<InstallRecordPatchedFile> files)
    {
        var restoredFiles = new List<UninstallRestoredFileResult>();
        var restoreAttemptBackups = new List<RestoreAttemptBackup>();
        var restoredBackups = new List<RestoreAttemptBackup>();

        try
        {
            foreach (InstallRecordPatchedFile file in files)
            {
                RestoreAttemptBackup restoreAttemptBackup = CreateRestoreAttemptBackup(file.AssetsFilePath);
                restoreAttemptBackups.Add(restoreAttemptBackup);

                RestorePatchedFile(file, restoreAttemptBackup);

                restoredBackups.Add(restoreAttemptBackup);
                restoredFiles.Add(new UninstallRestoredFileResult(
                    file.Target,
                    file.AssetsFilePath,
                    file.BackupPath));
            }
        }
        catch (Exception exception)
        {
            ThrowWithRecoveryFailures(exception, restoredBackups, restoreAttemptBackups);
        }

        ThrowIfCleanupFails(DeleteRestoreAttemptBackups(restoreAttemptBackups));

        return restoredFiles;
    }

    private static RestoreAttemptBackup CreateRestoreAttemptBackup(string assetsFilePath)
    {
        return new RestoreAttemptBackup(assetsFilePath, CreateRestoreAttemptBackupPath(assetsFilePath));
    }

    private static void RestorePatchedFile(InstallRecordPatchedFile file, RestoreAttemptBackup restoreAttemptBackup)
    {
        try
        {
            File.Copy(file.AssetsFilePath, restoreAttemptBackup.BackupPath, false);
            ModBackupStore.RestoreFile(file.BackupPath, file.AssetsFilePath);
        }
        catch (FileNotFoundException exception) when (!File.Exists(file.AssetsFilePath))
        {
            throw new FileNotFoundException(
                $"Assets file was deleted during uninstall: {file.AssetsFilePath}",
                file.AssetsFilePath,
                exception);
        }
        catch (FileNotFoundException exception)
        {
            throw new FileNotFoundException(
                $"Backup file was deleted during uninstall: {file.BackupPath}",
                file.BackupPath,
                exception);
        }
    }

    private static void ThrowWithRecoveryFailures(
        Exception restoreFailure,
        IReadOnlyList<RestoreAttemptBackup> restoredBackups,
        IReadOnlyList<RestoreAttemptBackup> restoreAttemptBackups)
    {
        var failures = new List<Exception> { restoreFailure };
        failures.AddRange(RollBackRestoredFiles(restoredBackups));
        failures.AddRange(DeleteRestoreAttemptBackups(restoreAttemptBackups));

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(restoreFailure).Throw();
        }

        throw new AggregateException(
            "Uninstall failed and one or more recovery steps also failed.",
            failures);
    }

    private static string CreateRestoreAttemptBackupPath(string path)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ??
                           throw new InvalidOperationException($"Cannot resolve assets file directory: {path}");
        string fileName = Path.GetFileName(path);

        string candidate;
        do
        {
            candidate = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.uninstall.tmp");
        } while (File.Exists(candidate));

        return candidate;
    }

    private static List<Exception> RollBackRestoredFiles(IReadOnlyList<RestoreAttemptBackup> restoreAttemptBackups)
    {
        var failures = new List<Exception>();

        for (int index = restoreAttemptBackups.Count - 1; index >= 0; index--)
        {
            RestoreAttemptBackup backup = restoreAttemptBackups[index];

            try
            {
                ModBackupStore.RestoreFile(backup.BackupPath, backup.AssetsFilePath);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    private static List<Exception> DeleteRestoreAttemptBackups(
        IReadOnlyList<RestoreAttemptBackup> restoreAttemptBackups)
    {
        var failures = new List<Exception>();

        foreach (RestoreAttemptBackup backup in restoreAttemptBackups)
        {
            try
            {
                if (File.Exists(backup.BackupPath))
                {
                    File.Delete(backup.BackupPath);
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    private static void ThrowIfCleanupFails(List<Exception> cleanupFailures)
    {
        if (cleanupFailures.Count > 0)
        {
            throw new AggregateException(
                "Uninstall restored assets but failed to clean up one or more temporary restore backups.",
                cleanupFailures);
        }
    }

    private sealed record RestoreAttemptBackup(string AssetsFilePath, string BackupPath);
}

public sealed record UninstallExecutionResult(
    IReadOnlyList<UninstallRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallDeletedFileResult> DeletedFiles);
