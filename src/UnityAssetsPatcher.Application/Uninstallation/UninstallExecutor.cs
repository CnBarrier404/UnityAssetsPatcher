using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed class UninstallExecutor
{
    private readonly BackupRepository _backupRepository;
    private readonly Action<string, string> _restoreFile;

    public UninstallExecutor(BackupRepository backupRepository) :
        this(backupRepository, BackupFileSystem.RestoreAtomically) { }

    public UninstallExecutor(BackupRepository backupRepository, Action<string, string> restoreFile)
    {
        ArgumentNullException.ThrowIfNull(backupRepository);
        ArgumentNullException.ThrowIfNull(restoreFile);
        _backupRepository = backupRepository;
        _restoreFile = restoreFile;
    }

    public UninstallModResult Execute(UninstallPlan plan)
    {
        UninstallResolvedPaths paths = UninstallPathValidator.ResolveRecordPaths(
            _backupRepository.BackupDirectory, plan.InstallDirectory, plan.GameDirectory, plan.Record);
        UninstallIntegrityInspector.EnsureSafeToUninstall(paths);
        ValidateUninstallAccess(paths, plan.InstallDirectory);

        BackupRepositoryMetadata repository = _backupRepository.LoadMetadata();
        string temporaryDirectory = _backupRepository.CreateTransactionDirectory();
        string rollbackDirectory = Path.Combine(temporaryDirectory, "rollback");
        Directory.CreateDirectory(rollbackDirectory);
        var files = new List<BackupTransactionFile>();
        bool transactionSaved = false;

        try
        {
            for (int index = 0; index < paths.PatchedFiles.Count; index++)
            {
                UninstallResolvedPatchedFile file = paths.PatchedFiles[index];
                string rollbackPath = Path.Combine(rollbackDirectory, $"assets-{index}.bin");
                File.Copy(file.AssetsFilePath, rollbackPath, false);
                if (!file.InstalledFile.Matches(rollbackPath))
                    throw new IOException($"Uninstall rollback snapshot verification failed: {file.AssetsFilePath}");
                files.Add(new BackupTransactionFile(BackupFileKind.Assets,
                    Path.GetRelativePath(paths.GameDirectory, file.AssetsFilePath), file.InstalledFile,
                    file.BackupFile, Path.GetRelativePath(temporaryDirectory, rollbackPath)));
            }

            for (int index = 0; index < paths.CopiedFiles.Count; index++)
            {
                UninstallResolvedCopiedFile file = paths.CopiedFiles[index];
                if (!File.Exists(file.DestinationPath)) continue;
                string rollbackPath = Path.Combine(rollbackDirectory, $"payload-{index}.bin");
                File.Copy(file.DestinationPath, rollbackPath, false);
                if (!file.InstalledFile.Matches(rollbackPath))
                    throw new IOException($"Payload rollback snapshot verification failed: {file.DestinationPath}");
                files.Add(new BackupTransactionFile(BackupFileKind.Payload,
                    Path.GetRelativePath(paths.GameDirectory, file.DestinationPath), file.InstalledFile, null,
                    Path.GetRelativePath(temporaryDirectory, rollbackPath)));
            }

            var transaction = new BackupTransaction(repository.RepositoryId, BackupOperationKind.Uninstall,
                plan.Record.Id,
                paths.GameDirectory, plan.Record.GameInstanceFingerprint, files);
            BackupTransactionStore.Save(temporaryDirectory, transaction);
            transactionSaved = true;

            var restoredFiles = new List<UninstallRestoredFileResult>();
            foreach (UninstallResolvedPatchedFile file in paths.PatchedFiles)
            {
                if (!file.InstalledFile.Matches(file.AssetsFilePath))
                    throw new IOException($"Assets file changed during uninstall: {file.AssetsFilePath}");
                _restoreFile(file.BackupPath, file.AssetsFilePath);
                if (!file.BackupFile.Matches(file.AssetsFilePath))
                    throw new IOException($"Restored assets verification failed: {file.AssetsFilePath}");
                restoredFiles.Add(new UninstallRestoredFileResult(file.Target, file.AssetsFilePath));
            }

            var deletedFiles = new List<UninstallDeletedFileResult>();
            foreach (UninstallResolvedCopiedFile file in paths.CopiedFiles)
            {
                if (!File.Exists(file.DestinationPath))
                {
                    deletedFiles.Add(new UninstallDeletedFileResult(file.DestinationPath, false));
                    continue;
                }

                if (!file.InstalledFile.Matches(file.DestinationPath))
                    throw new IOException($"Payload changed during uninstall: {file.DestinationPath}");
                File.Delete(file.DestinationPath);
                deletedFiles.Add(new UninstallDeletedFileResult(file.DestinationPath, true));
            }

            string removedInstall = Path.Combine(temporaryDirectory, "removed-install");
            Directory.Move(plan.InstallDirectory, removedInstall);
            Directory.Delete(temporaryDirectory, true);
            return new UninstallModResult(plan.Record.Id, plan.Record.ModName, plan.Record.ModVersion,
                restoredFiles, deletedFiles);
        }
        catch (Exception failure)
        {
            if (!transactionSaved)
            {
                if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
                throw;
            }

            BackupRecoveryReport recovery = _backupRepository.RecoverUnderLock();
            if (recovery.Status == BackupRepositoryStatus.Locked)
                throw new BackupRecoveryException("Uninstall failed and automatic rollback was unsafe.", recovery,
                    failure);
            throw;
        }
    }

    private static void ValidateUninstallAccess(UninstallResolvedPaths paths, string installDirectory)
    {
        foreach (UninstallResolvedPatchedFile file in paths.PatchedFiles)
        {
            using FileStream target = File.Open(file.AssetsFilePath, FileMode.Open, FileAccess.ReadWrite,
                FileShare.None);
            using FileStream backup = File.Open(file.BackupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        foreach (UninstallResolvedCopiedFile file in paths.CopiedFiles)
        {
            if (File.Exists(file.DestinationPath))
                using (File.Open(file.DestinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        using FileStream record = File.Open(Path.Combine(installDirectory, "record.json"), FileMode.Open,
            FileAccess.Read, FileShare.Read);
    }
}
