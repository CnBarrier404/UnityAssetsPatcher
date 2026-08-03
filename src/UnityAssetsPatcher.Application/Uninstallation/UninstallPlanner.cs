using UnityAssetsPatcher.Application.IO;
using InstallRecordSummary = UnityAssetsPatcher.Application.Contracts.InstallRecordSummary;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed record UninstallPlan(
    string InstallDirectory,
    string GameDirectory,
    InstallRecord Record);

public sealed class UninstallPlanner
{
    private readonly BackupRepository _backupRepository;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly IFileSystemOperations _fileSystemOperations;

    public UninstallPlanner(
        BackupRepository backupRepository,
        GameDirectoryResolver gameDirectoryResolver,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(backupRepository);
        ArgumentNullException.ThrowIfNull(gameDirectoryResolver);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _backupRepository = backupRepository;
        _gameDirectoryResolver = gameDirectoryResolver;
        _fileSystemOperations = fileSystemOperations;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return _backupRepository.ListInstalled()
            .Select(record => new InstallRecordSummary(
                record.Id,
                record.ModName,
                record.ModVersion,
                record.GameName,
                record.InstalledAt))
            .ToArray();
    }

    public UninstallPreviewResult BuildPreview(UninstallPreviewRequest request)
    {
        InstallRecordEntry entry = ResolveRecord(request.InstallId);
        string installDirectory = entry.InstallDirectory;
        InstallRecord record = entry.Record;
        string gameDirectory = ResolveGameDirectory(request.GameDirectory, record);
        ValidateGameInstance(record, gameDirectory);
        var blockers = InstallLayerAnalyzer.FindBlockingRecords(
            record, _backupRepository.ListRecords());
        UninstallResolvedPaths paths = UninstallPathValidator.ResolveRecordPaths(
            _fileSystemOperations,
            _backupRepository.BackupDirectory,
            installDirectory,
            gameDirectory,
            record);

        var restoredFiles = paths.PatchedFiles
            .Select(file => new UninstallPreviewRestoredFileResult(
                file.Target,
                UninstallIntegrityInspector.Inspect(_fileSystemOperations, file.AssetsFilePath, file.InstalledFile),
                UninstallIntegrityInspector.Inspect(_fileSystemOperations, file.BackupPath, file.BackupFile)))
            .ToArray();

        var deletedFiles = paths.CopiedFiles
            .Select(file => new UninstallPreviewDeletedFileResult(
                file.DestinationPath,
                UninstallIntegrityInspector.Inspect(_fileSystemOperations, file.DestinationPath, file.InstalledFile)))
            .ToArray();

        bool canUninstall = blockers.Count == 0 &&
                            restoredFiles.All(file =>
                                file is
                                {
                                    TargetStatus: FileIntegrityStatus.Matches,
                                    BackupStatus: FileIntegrityStatus.Matches,
                                }) &&
                            deletedFiles.All(file =>
                                file.Status is FileIntegrityStatus.Matches or FileIntegrityStatus.Missing);

        return new UninstallPreviewResult(
            record.Id,
            record.ModName,
            record.ModVersion,
            record.InstalledAt,
            paths.GameDirectory,
            canUninstall,
            blockers.Select(blocker => new UninstallBlockingModResult(
                blocker.Record.ModName,
                blocker.Record.ModVersion,
                blocker.Record.InstalledAt,
                blocker.OverlappingAssetsFiles)).ToArray(),
            restoredFiles,
            deletedFiles);
    }

    public UninstallPlan BuildUninstall(UninstallModRequest request)
    {
        InstallRecordEntry entry = ResolveRecord(request.InstallId);
        string installDirectory = entry.InstallDirectory;
        InstallRecord record = entry.Record;
        string gameDirectory = ResolveGameDirectory(request.GameDirectory, record);
        ValidateGameInstance(record, gameDirectory);

        var blockers = InstallLayerAnalyzer.FindBlockingRecords(
            record, _backupRepository.ListRecords());

        if (blockers.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot uninstall {record.ModName} because later installed mods depend on the same assets files: " +
                string.Join(", ", blockers.Select(blocker => blocker.Record.ModName)));
        }

        UninstallResolvedPaths paths = UninstallPathValidator.ResolveRecordPaths(
            _fileSystemOperations,
            _backupRepository.BackupDirectory,
            installDirectory,
            gameDirectory,
            record);

        UninstallIntegrityInspector.EnsureSafeToUninstall(_fileSystemOperations, paths);

        return new UninstallPlan(
            installDirectory,
            paths.GameDirectory,
            record);
    }

    private InstallRecordEntry ResolveRecord(string installId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installId);

        InstallRecordEntry[] matches = _backupRepository.ListRecords()
            .Where(entry => string.Equals(entry.Record.Id, installId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 1)
        {
            return matches[0];
        }

        if (matches.Length > 1)
        {
            throw new InvalidOperationException($"Multiple install records use ID '{installId}'.");
        }

        throw new KeyNotFoundException($"Install record not found: {installId}");
    }

    private void ValidateGameInstance(InstallRecord record, string gameDirectory)
    {
        string fingerprint = GameInstanceIdentity.CreateFingerprint(_fileSystemOperations, gameDirectory);
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
