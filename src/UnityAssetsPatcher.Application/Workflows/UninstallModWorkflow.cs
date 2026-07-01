using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class UninstallModWorkflow
{
    private readonly ModBackupStore _backupStore;

    public UninstallModWorkflow(ModBackupStore backupStore)
    {
        _backupStore = backupStore;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return _backupStore.ListInstalled();
    }

    public UninstallPreviewResult Preview(UninstallPreviewRequest request)
    {
        InstallRecord record = _backupStore.Load(request.InstallDirectory);

        if (record.Status != InstallRecordStatus.Installed)
        {
            throw new InvalidOperationException("Install record is not currently installed.");
        }

        UninstallPreviewRestoredFileResult[] restoredFiles = record.PatchedFiles
            .Select(file => new UninstallPreviewRestoredFileResult(
                file.Target,
                file.AssetsFilePath,
                file.BackupPath,
                File.Exists(file.AssetsFilePath),
                File.Exists(file.BackupPath)))
            .ToArray();
        UninstallPreviewDeletedFileResult[] deletedFiles = record.CopiedFiles
            .Select(file => new UninstallPreviewDeletedFileResult(
                file.Source,
                file.DestinationPath,
                File.Exists(file.DestinationPath)))
            .ToArray();
        bool canUninstall = restoredFiles.All(file => file.TargetExists && file.BackupExists);

        return new UninstallPreviewResult(
            record.ModName,
            record.ModVersion,
            record.ModAuthor,
            canUninstall,
            restoredFiles,
            deletedFiles);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        InstallRecord record = _backupStore.Load(request.InstallDirectory);

        if (record.Status != InstallRecordStatus.Installed)
        {
            throw new InvalidOperationException("Install record is not currently installed.");
        }

        var restoredFiles = new List<UninstallRestoredFileResult>();

        foreach (InstallRecordPatchedFile file in record.PatchedFiles)
        {
            if (!File.Exists(file.AssetsFilePath))
            {
                throw new FileNotFoundException(
                    $"Assets file was deleted during uninstall: {file.AssetsFilePath}",
                    file.AssetsFilePath);
            }

            try
            {
                ModBackupStore.RestoreFile(file.BackupPath, file.AssetsFilePath);
            }
            catch (FileNotFoundException)
            {
                throw new FileNotFoundException(
                    $"Backup file was deleted during uninstall: {file.BackupPath}",
                    file.BackupPath);
            }

            restoredFiles.Add(new UninstallRestoredFileResult(
                file.Target,
                file.AssetsFilePath,
                file.BackupPath));
        }

        var deletedFiles = new List<UninstallDeletedFileResult>();

        foreach (InstallRecordCopiedFile file in record.CopiedFiles)
        {
            if (!File.Exists(file.DestinationPath))
            {
                deletedFiles.Add(new UninstallDeletedFileResult(file.Source, file.DestinationPath, false));
                continue;
            }

            File.Delete(file.DestinationPath);
            deletedFiles.Add(new UninstallDeletedFileResult(file.Source, file.DestinationPath, true));
        }

        Directory.Delete(request.InstallDirectory, true);

        return new UninstallModResult(
            record.ModName,
            record.ModVersion,
            record.ModAuthor,
            restoredFiles,
            deletedFiles);
    }
}
