using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed class UninstallPlanner
{
    private readonly ModBackupStore _backupStore;
    private readonly GameDirectoryResolver _gameDirectoryResolver;

    public UninstallPlanner(ModBackupStore backupStore, GameDirectoryResolver gameDirectoryResolver)
    {
        _backupStore = backupStore;
        _gameDirectoryResolver = gameDirectoryResolver;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return _backupStore.ListInstalled();
    }

    public UninstallPreviewPlan BuildPreview(UninstallPreviewRequest request)
    {
        UninstallPathValidator.ValidateInstallDirectory(_backupStore.BackupDirectory, request.InstallDirectory);

        InstallRecord record = _backupStore.Load(request.InstallDirectory);
        UninstallResolvedPaths paths = UninstallPathValidator.ResolveRecordPaths(
            _backupStore.BackupDirectory,
            request.InstallDirectory,
            ResolveGameDirectory(request.GameDirectory, record),
            record);

        var restoredFiles = paths.PatchedFiles
            .Select(file => new UninstallPreviewRestoredFileResult(
                file.Target,
                file.AssetsFilePath,
                file.BackupPath,
                File.Exists(file.AssetsFilePath),
                File.Exists(file.BackupPath)))
            .ToArray();

        var deletedFiles = paths.CopiedFiles
            .Select(file => new UninstallPreviewDeletedFileResult(
                file.Source,
                file.DestinationPath,
                File.Exists(file.DestinationPath)))
            .ToArray();

        bool canUninstall = restoredFiles.All(file => file is { TargetExists: true, BackupExists: true });

        return new UninstallPreviewPlan(
            record,
            paths.GameDirectory,
            canUninstall,
            restoredFiles,
            deletedFiles);
    }

    public UninstallPlan BuildUninstall(UninstallModRequest request)
    {
        UninstallPathValidator.ValidateInstallDirectory(_backupStore.BackupDirectory, request.InstallDirectory);

        InstallRecord record = _backupStore.Load(request.InstallDirectory);
        UninstallResolvedPaths paths = UninstallPathValidator.ResolveRecordPaths(
            _backupStore.BackupDirectory,
            request.InstallDirectory,
            request.GameDirectory,
            record);

        return new UninstallPlan(
            _backupStore.BackupDirectory,
            request.InstallDirectory,
            paths.GameDirectory,
            record);
    }

    private string ResolveGameDirectory(string? requestedGameDirectory, InstallRecord record)
    {
        if (requestedGameDirectory is null && string.IsNullOrWhiteSpace(record.GameName))
        {
            throw new DirectoryNotFoundException(
                "Game directory was not provided and install record does not contain a game name.");
        }

        return _gameDirectoryResolver.ResolveRequired(requestedGameDirectory, record.GameName);
    }
}

public sealed record UninstallPreviewPlan(
    InstallRecord Record,
    string GameDirectory,
    bool CanUninstall,
    IReadOnlyList<UninstallPreviewRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallPreviewDeletedFileResult> DeletedFiles);

public sealed record UninstallPlan(
    string BackupDirectory,
    string InstallDirectory,
    string GameDirectory,
    InstallRecord Record);
