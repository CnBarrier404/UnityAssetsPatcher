using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal static partial class UpdateLog
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Debug,
        Message = "Skipping update check because current version {CurrentVersion} is not a release version")]
    public static partial void UpdateCheckSkipped(ILogger logger, string currentVersion);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Checking for updates from {ManifestUrl}")]
    public static partial void CheckingForUpdate(ILogger logger, string manifestUrl);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Debug,
        Message = "No update is available; current version is {CurrentVersion} and latest version is {LatestVersion}")]
    public static partial void NoUpdateAvailable(ILogger logger, string currentVersion, string latestVersion);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Update {LatestVersion} is available for current version {CurrentVersion}")]
    public static partial void UpdateAvailable(ILogger logger, string currentVersion, string latestVersion);

    [LoggerMessage(
        EventId = 3090,
        Level = LogLevel.Debug,
        Message = "Update request returned HTTP status {StatusCode}")]
    public static partial void UpdateRequestRejected(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 3091,
        Level = LogLevel.Warning,
        Message = "Update manifest was rejected")]
    public static partial void UpdateManifestRejected(ILogger logger);

    [LoggerMessage(
        EventId = 3092,
        Level = LogLevel.Debug,
        Message = "Update request failed")]
    public static partial void UpdateRequestFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3093,
        Level = LogLevel.Debug,
        Message = "Update check was canceled")]
    public static partial void UpdateCheckCanceled(ILogger logger);
}
