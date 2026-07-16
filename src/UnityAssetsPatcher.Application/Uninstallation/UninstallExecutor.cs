using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed class UninstallExecutor
{
    private readonly Action<string, string> _restoreFile;

    public UninstallExecutor() : this(ModBackupStore.RestoreFile) { }

    public UninstallExecutor(Action<string, string> restoreFile)
    {
        ArgumentNullException.ThrowIfNull(restoreFile);
        _restoreFile = restoreFile;
    }

    public UninstallModResult Execute(UninstallPlan plan)
    {
        UninstallResolvedPaths paths = UninstallPathValidator.ResolveRecordPaths(
            plan.BackupDirectory,
            plan.InstallDirectory,
            plan.GameDirectory,
            plan.Record);

        UninstallIntegrityInspector.EnsureSafeToUninstall(paths);
        ValidateUninstallAccess(paths, plan.InstallDirectory);

        string stagingDirectory = Path.Combine(plan.InstallDirectory, ".uninstall-staging");
        var patched = paths.PatchedFiles.Select(file => new JournalPatchedFile(
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
                _restoreFile(file.BackupPath, file.AssetsFilePath);
                restoredFiles.Add(new UninstallRestoredFileResult(file.Target, file.AssetsFilePath));
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

            return new UninstallModResult(
                plan.Record.Id,
                plan.Record.ModName,
                plan.Record.ModVersion,
                restoredFiles,
                deletedFiles);
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
                        _restoreFile(file.RollbackPath!, file.AssetsFilePath);
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
                        _restoreFile(file.StagingPath!, file.DestinationPath);
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
            throw;
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
                deletedFiles.Add(new UninstallDeletedFileResult(file.DestinationPath, false));

                continue;
            }

            File.Delete(file.DestinationPath);
            deletedFiles.Add(new UninstallDeletedFileResult(file.DestinationPath, true));
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
}
