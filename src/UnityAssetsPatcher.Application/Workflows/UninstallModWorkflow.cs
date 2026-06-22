using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Core.IO;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class UninstallModWorkflow
{
    private readonly ModInstallationStore _recordStore;

    public UninstallModWorkflow(ModInstallationStore recordStore)
    {
        _recordStore = recordStore;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return _recordStore.ListInstalled();
    }

    public UninstallPreviewResult Preview(UninstallPreviewRequest request)
    {
        InstallRecord record = _recordStore.Load(request.InstallDirectory);

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
        InstallRecord record = _recordStore.Load(request.InstallDirectory);

        if (record.Status != InstallRecordStatus.Installed)
        {
            throw new InvalidOperationException("Install record is not currently installed.");
        }

        ValidateFiles(record);

        string uninstallDirectory = Path.Combine(request.InstallDirectory, "uninstall");
        Directory.CreateDirectory(uninstallDirectory);
        var restoredFiles = new List<UninstallRestoredFileResult>();

        foreach (InstallRecordPatchedFile file in record.PatchedFiles)
        {
            string uninstallBackupPath = CreateUniqueFilePath(uninstallDirectory, file.AssetsFilePath);
            File.Copy(file.AssetsFilePath, uninstallBackupPath, false);
            RestoreBackup(file.BackupPath, file.AssetsFilePath);
            restoredFiles.Add(new UninstallRestoredFileResult(
                file.Target,
                file.AssetsFilePath,
                file.BackupPath,
                uninstallBackupPath));
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

        InstallRecord updated = record with
        {
            Status = InstallRecordStatus.Uninstalled,
            UninstalledAt = DateTimeOffset.Now,
            PatchedFiles = record.PatchedFiles
                .Select(file =>
                {
                    UninstallRestoredFileResult restored = restoredFiles.Single(restoredFile => string.Equals(
                        restoredFile.AssetsFilePath,
                        file.AssetsFilePath,
                        StringComparison.OrdinalIgnoreCase));

                    return file with { UninstallBackupPath = restored.UninstallBackupPath };
                })
                .ToArray(),
            CopiedFiles = record.CopiedFiles
                .Select(file => file with { Exists = File.Exists(file.DestinationPath) })
                .ToArray(),
        };
        _recordStore.Save(updated, request.InstallDirectory);

        return new UninstallModResult(
            record.ModName,
            record.ModVersion,
            record.ModAuthor,
            restoredFiles,
            deletedFiles);
    }

    private static void ValidateFiles(InstallRecord record)
    {
        foreach (InstallRecordPatchedFile file in record.PatchedFiles)
        {
            if (!File.Exists(file.AssetsFilePath))
            {
                throw new FileNotFoundException(
                    $"Installed assets file not found: {file.AssetsFilePath}",
                    file.AssetsFilePath);
            }

            if (!File.Exists(file.BackupPath))
            {
                throw new FileNotFoundException(
                    $"Install backup file not found: {file.BackupPath}",
                    file.BackupPath);
            }
        }
    }

    private static void RestoreBackup(string backupPath, string assetsFilePath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(assetsFilePath)) ??
                           throw new InvalidOperationException(
                               $"Cannot resolve assets file directory: {assetsFilePath}");
        string tempPath = Path.Combine(directory, $"{Path.GetFileName(assetsFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(backupPath, tempPath, false);
            FileHelper.SafeMoveFile(tempPath, assetsFilePath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string CreateUniqueFilePath(string directory, string originalPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(originalPath);
        string extension = Path.GetExtension(originalPath);
        string candidate = Path.Combine(directory, $"{fileName}{extension}");

        for (int index = 1; File.Exists(candidate); index++)
        {
            candidate = Path.Combine(directory, $"{fileName}.{index}{extension}");
        }

        return candidate;
    }
}
