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

    [LoggerMessage(
        EventId = 3004,
        EventName = nameof(UpdateRequestFailed),
        Level = LogLevel.Warning,
        Message = "Update check failed: {ErrorMessage}.")]
    public static partial void UpdateRequestFailed(
        ILogger logger,
        Exception exception,
        string errorMessage);

    [LoggerMessage(
        EventId = 3005,
        EventName = nameof(UpdateRequestRejected),
        Level = LogLevel.Warning,
        Message = "Update check failed: the request returned HTTP status {StatusCode}.")]
    public static partial void UpdateRequestRejected(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 3006,
        EventName = nameof(UpdateManifestRejected),
        Level = LogLevel.Warning,
        Message = "Update check failed: manifest was rejected because it does not match the expected format.")]
    public static partial void UpdateManifestRejected(ILogger logger);

    [LoggerMessage(
        EventId = 3007,
        EventName = nameof(UpdateManifestRejectedAsInvalidJson),
        Level = LogLevel.Warning,
        Message = "Update check failed: manifest was rejected because it is not valid JSON.")]
    public static partial void UpdateManifestRejectedAsInvalidJson(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3008,
        EventName = nameof(UpdateManifestRejectedAsTooLarge),
        Level = LogLevel.Warning,
        Message =
            "Update check failed: manifest was rejected because it exceeds the maximum size of {MaximumSize} bytes.")]
    public static partial void UpdateManifestRejectedAsTooLarge(ILogger logger, int maximumSize);

    [LoggerMessage(
        EventId = 3009,
        EventName = nameof(UpdateCheckCanceled),
        Level = LogLevel.Debug,
        Message = "Update check was canceled.")]
    public static partial void UpdateCheckCanceled(ILogger logger);
}
