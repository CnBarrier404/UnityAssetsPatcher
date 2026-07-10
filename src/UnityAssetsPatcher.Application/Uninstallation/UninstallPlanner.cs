using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed record UninstallPreviewPlan(
    InstallRecord Record,
    string GameDirectory,
    bool CanUninstall,
    IReadOnlyList<BlockingInstallRecord> BlockingRecords,
    IReadOnlyList<UninstallPreviewRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallPreviewDeletedFileResult> DeletedFiles);

public sealed record UninstallPlan(
    string BackupDirectory,
    string InstallDirectory,
    string GameDirectory,
    InstallRecord Record);

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
        string gameDirectory = ResolveGameDirectory(request.GameDirectory, record);
        ValidateGameInstance(record, gameDirectory);
        var blockers = InstallLayerAnalyzer.FindBlockingRecords(
            record, _backupStore.ListRecords());
        UninstallResolvedPaths paths = UninstallPathValidator.ResolveRecordPaths(
            _backupStore.BackupDirectory,
            request.InstallDirectory,
            gameDirectory,
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

        bool canUninstall = blockers.Count == 0 &&
                            restoredFiles.All(file => file is { TargetExists: true, BackupExists: true });

        return new UninstallPreviewPlan(
            record,
            paths.GameDirectory,
            canUninstall,
            blockers,
            restoredFiles,
            deletedFiles);
    }

    public UninstallPlan BuildUninstall(UninstallModRequest request)
    {
        UninstallPathValidator.ValidateInstallDirectory(_backupStore.BackupDirectory, request.InstallDirectory);

        InstallRecord record = _backupStore.Load(request.InstallDirectory);
        ValidateGameInstance(record, request.GameDirectory);

        var blockers = InstallLayerAnalyzer.FindBlockingRecords(
            record, _backupStore.ListRecords());

        if (blockers.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot uninstall {record.ModName} because later installed mods depend on the same assets files: " +
                string.Join(", ", blockers.Select(blocker => blocker.Entry.Record.ModName)));
        }

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

    private static void ValidateGameInstance(InstallRecord record, string gameDirectory)
    {
        string fingerprint = GameInstanceIdentity.CreateFingerprint(gameDirectory);
        if (!string.Equals(record.GameInstanceFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The selected game directory does not match the install record game instance.");
        }
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
