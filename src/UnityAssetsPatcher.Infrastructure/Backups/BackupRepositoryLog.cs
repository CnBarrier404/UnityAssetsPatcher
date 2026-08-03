using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.Backups;

internal static partial class BackupRepositoryLog
{
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Information,
        Message = "Initialized backup repository at {RepositoryDirectory} with repository ID {RepositoryId}")]
    public static partial void RepositoryInitialized(
        ILogger logger,
        string repositoryDirectory,
        string repositoryId);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Debug,
        Message = "Loaded backup repository {RepositoryId} from {RepositoryDirectory}")]
    public static partial void RepositoryLoaded(
        ILogger logger,
        string repositoryDirectory,
        string repositoryId);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Debug,
        Message = "Loaded {InstallCount} install records from backup repository {RepositoryId}")]
    public static partial void InstallRecordsLoaded(ILogger logger, int installCount, string repositoryId);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Debug,
        Message = "Wrote install record {InstallId} to {InstallDirectory}")]
    public static partial void InstallRecordWritten(ILogger logger, string installId, string installDirectory);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Information,
        Message = "Committed install record {InstallId}")]
    public static partial void InstallCommitted(ILogger logger, string installId);

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Debug,
        Message = "Stored verified backup of {SourcePath} at {BackupPath}")]
    public static partial void BackupStored(ILogger logger, string sourcePath, string backupPath);
}
