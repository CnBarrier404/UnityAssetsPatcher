using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal static partial class UpdateLog
{
    [LoggerMessage(
        EventId = 3000,
        EventName = nameof(UpdateCheckSkipped),
        Level = LogLevel.Information,
        Message = "Skipping update check because current version is a development version.")]
    public static partial void UpdateCheckSkipped(ILogger logger);

    [LoggerMessage(
        EventId = 3001,
        EventName = nameof(UpdateCheckStarted),
        Level = LogLevel.Information,
        Message = "Checking for updates...")]
    public static partial void UpdateCheckStarted(ILogger logger);

    [LoggerMessage(
        EventId = 3002,
        EventName = nameof(UpdateCheckCompletedWithoutUpdate),
        Level = LogLevel.Information,
        Message =
            "Update check completed: no update available (current version {CurrentVersion}, latest version {LatestVersion}).")]
    public static partial void UpdateCheckCompletedWithoutUpdate(
        ILogger logger,
        string currentVersion,
        string latestVersion);

    [LoggerMessage(
        EventId = 3003,
        EventName = nameof(UpdateCheckCompletedWithUpdate),
        Level = LogLevel.Information,
        Message =
            "Update check completed: new version {LatestVersion} is available (current version {CurrentVersion}).")]
    public static partial void UpdateCheckCompletedWithUpdate(
        ILogger logger,
        string currentVersion,
        string latestVersion);
}
