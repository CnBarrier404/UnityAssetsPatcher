using System.Runtime.ExceptionServices;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed class UninstallExecutor
{
    public UninstallExecutionResult Execute(UninstallPlan plan)
    {
        UninstallResolvedPaths paths = UninstallPathValidator.ResolveRecordPaths(
            plan.BackupDirectory,
            plan.InstallDirectory,
            plan.GameDirectory,
            plan.Record);

        UninstallIntegrityInspector.EnsureSafeToUninstall(paths);
        ValidateUninstallAccess(paths, plan.InstallDirectory);

        string stagingDirectory = Path.Combine(plan.InstallDirectory, ".uninstall-staging");
        var patched = paths.PatchedFiles.Select((file, index) => new JournalPatchedFile(
            file.AssetsFilePath, file.BackupPath, CreateRestoreAttemptBackupPath(file.AssetsFilePath))).ToArray();
        var payload = paths.CopiedFiles.Select((file, index) => new JournalPayloadFile(
            file.DestinationPath, Path.Combine(stagingDirectory, $"payload-{index}.rollback"))).ToArray();
        var journal = new OperationJournal(
            OperationJournalStore.CurrentFormatVersion, OperationKind.Uninstall, OperationPhase.Pending,
            paths.GameDirectory, patched, payload);
        OperationJournalStore.Save(plan.InstallDirectory, journal);

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            for (int index = 0; index < paths.PatchedFiles.Count; index++)
            {
                File.Copy(paths.PatchedFiles[index].AssetsFilePath, patched[index].RollbackPath!, false);
            }

            for (int index = 0; index < paths.CopiedFiles.Count; index++)
            {
                if (File.Exists(paths.CopiedFiles[index].DestinationPath))
                {
                    File.Copy(paths.CopiedFiles[index].DestinationPath, payload[index].StagingPath!, false);
                }
            }

            var restoredFiles = new List<UninstallRestoredFileResult>();

            foreach (UninstallResolvedPatchedFile file in paths.PatchedFiles)
            {
                ModBackupStore.RestoreFile(file.BackupPath, file.AssetsFilePath);
                restoredFiles.Add(new UninstallRestoredFileResult(file.Target, file.AssetsFilePath, file.BackupPath));
            }

            journal = journal with { Phase = OperationPhase.AssetsChanged };
            OperationJournalStore.Save(plan.InstallDirectory, journal);

            var deletedFiles = DeleteCopiedFiles(paths.CopiedFiles);
            journal = journal with { Phase = OperationPhase.PayloadChanged };

            OperationJournalStore.Save(plan.InstallDirectory, journal);

            journal = journal with { Phase = OperationPhase.Committed };

            OperationJournalStore.Save(plan.InstallDirectory, journal);
            ModBackupStore.DeleteRecord(plan.InstallDirectory);

            foreach (JournalPatchedFile file in patched)
            {
                if (File.Exists(file.RollbackPath))
                {
                    File.Delete(file.RollbackPath!);
                }
            }

            Directory.Delete(plan.InstallDirectory, true);

            return new UninstallExecutionResult(restoredFiles, deletedFiles);
        }
        catch (Exception failure)
        {
            if (journal.Phase == OperationPhase.Committed)
            {
                throw new AggregateException(
                    "Uninstall committed but cleanup is incomplete; startup recovery will finish it.",
                    failure,
                    new InvalidOperationException("Committed uninstall cleanup remains pending."));
            }

            var recoveryFailures = new List<Exception>();

            foreach (JournalPatchedFile file in patched.Reverse())
            {
                try
                {
                    if (File.Exists(file.RollbackPath))
                    {
                        ModBackupStore.RestoreFile(file.RollbackPath!, file.AssetsFilePath);
                    }
                }
                catch (Exception exception)
                {
                    recoveryFailures.Add(exception);
                }
            }

            foreach (JournalPayloadFile file in payload.Reverse())
            {
                try
                {
                    if (File.Exists(file.StagingPath) && !File.Exists(file.DestinationPath))
                    {
                        ModBackupStore.RestoreFile(file.StagingPath!, file.DestinationPath);
                    }
                }
                catch (Exception exception)
                {
                    recoveryFailures.Add(exception);
                }
            }

            if (recoveryFailures.Count != 0)
            {
                throw new AggregateException("Uninstall failed and one or more recovery steps also failed.",
                    new[] { failure }.Concat(recoveryFailures));
            }

            foreach (JournalPatchedFile file in patched)
            {
                if (File.Exists(file.RollbackPath))
                {
                    File.Delete(file.RollbackPath!);
                }
            }

            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, true);
            }

            OperationJournalStore.Delete(plan.InstallDirectory);
            ExceptionDispatchInfo.Capture(failure).Throw();

            throw new AggregateException("Uninstall failed and one or more recovery steps also failed.",
                new[] { failure }.Concat(recoveryFailures));
        }
    }

    private static void ValidateUninstallAccess(UninstallResolvedPaths paths, string installDirectory)
    {
        ValidateRestorableFiles(paths.PatchedFiles);

        foreach (UninstallResolvedPatchedFile file in paths.PatchedFiles)
        {
            EnsureCanOpen(file.AssetsFilePath, FileAccess.ReadWrite, FileShare.None);
            EnsureCanOpen(file.BackupPath, FileAccess.Read, FileShare.Read);
        }

        foreach (UninstallResolvedCopiedFile file in paths.CopiedFiles)
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

    private static List<UninstallDeletedFileResult> DeleteCopiedFiles(IReadOnlyList<UninstallResolvedCopiedFile> files)
    {
        var deletedFiles = new List<UninstallDeletedFileResult>();

        foreach (UninstallResolvedCopiedFile file in files)
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

    private static void ValidateRestorableFiles(IReadOnlyList<UninstallResolvedPatchedFile> files)
    {
        foreach (UninstallResolvedPatchedFile file in files)
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
        IReadOnlyList<UninstallResolvedPatchedFile> files)
    {
        var restoredFiles = new List<UninstallRestoredFileResult>();
        var restoreAttemptBackups = new List<RestoreAttemptBackup>();
        var restoredBackups = new List<RestoreAttemptBackup>();

        try
        {
            foreach (UninstallResolvedPatchedFile file in files)
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

    private static void RestorePatchedFile(UninstallResolvedPatchedFile file, RestoreAttemptBackup restoreAttemptBackup)
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
