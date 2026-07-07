using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed class UninstallPlanner
{
    private readonly ModBackupStore _backupStore;

    public UninstallPlanner(ModBackupStore backupStore)
    {
        _backupStore = backupStore;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return _backupStore.ListInstalled();
    }

    public UninstallPreviewPlan BuildPreview(UninstallPreviewRequest request)
    {
        InstallRecord record = _backupStore.Load(request.InstallDirectory);

        var restoredFiles = record.PatchedFiles
            .Select(file => new UninstallPreviewRestoredFileResult(
                file.Target,
                file.AssetsFilePath,
                file.BackupPath,
                File.Exists(file.AssetsFilePath),
                File.Exists(file.BackupPath)))
            .ToArray();

        var deletedFiles = record.CopiedFiles
            .Select(file => new UninstallPreviewDeletedFileResult(
                file.Source,
                file.DestinationPath,
                File.Exists(file.DestinationPath)))
            .ToArray();

        bool canUninstall = restoredFiles.All(file => file is { TargetExists: true, BackupExists: true });

        return new UninstallPreviewPlan(record, canUninstall, restoredFiles, deletedFiles);
    }

    public UninstallPlan BuildUninstall(UninstallModRequest request)
    {
        InstallRecord record = _backupStore.Load(request.InstallDirectory);

        return new UninstallPlan(request.InstallDirectory, record);
    }
}

public sealed record UninstallPreviewPlan(
    InstallRecord Record,
    bool CanUninstall,
    IReadOnlyList<UninstallPreviewRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallPreviewDeletedFileResult> DeletedFiles);

public sealed record UninstallPlan(string InstallDirectory, InstallRecord Record);
