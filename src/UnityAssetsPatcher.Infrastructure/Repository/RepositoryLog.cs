using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal static partial class RepositoryLog
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
}
