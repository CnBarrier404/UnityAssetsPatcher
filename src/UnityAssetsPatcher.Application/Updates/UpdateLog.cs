using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Application.Updates;

internal static partial class UpdateLog
{
    [LoggerMessage(
        EventId = 3000,
        EventName = nameof(UpdateCheckSkipped),
        Level = LogLevel.Information,
        Message = "Skipping update check because current version is a development version")]
    public static partial void UpdateCheckSkipped(ILogger logger);

    [LoggerMessage(
        EventId = 3001,
        EventName = nameof(UpdateCheckStarted),
        Level = LogLevel.Information,
        Message = "Checking update...")]
    public static partial void UpdateCheckStarted(ILogger logger);

    [LoggerMessage(
        EventId = 3002,
        EventName = nameof(UpdateCheckCompletedWithoutUpdate),
        Level = LogLevel.Information,
        Message =
            "Update check completed: no update available")]
    public static partial void UpdateCheckCompletedWithoutUpdate(ILogger logger);

    [LoggerMessage(
        EventId = 3003,
        EventName = nameof(UpdateCheckCompletedWithUpdate),
        Level = LogLevel.Information,
        Message =
            "Update check completed: new version is available: {CurrentVersion} -> {LatestVersion}")]
    public static partial void UpdateCheckCompletedWithUpdate(
        ILogger logger,
        string currentVersion,
        string latestVersion);

    [LoggerMessage(
        EventId = 3004,
        EventName = nameof(UpdateRequestFailed),
        Level = LogLevel.Warning,
        Message = "Update check failed: GitHub API network request failed ({RequestError}): {ErrorMessage}")]
    public static partial void
        UpdateRequestFailed(ILogger logger, Exception exception, HttpRequestError requestError, string errorMessage);

    [LoggerMessage(
        EventId = 3005,
        EventName = nameof(UpdateRequestRejected),
        Level = LogLevel.Warning,
        Message = "Update check failed: GitHub API returned HTTP {StatusCode} ({Reason})")]
    public static partial void
        UpdateRequestRejected(ILogger logger, Exception exception, int statusCode, string reason);

    [LoggerMessage(
        EventId = 3006,
        EventName = nameof(UpdateResponseRejected),
        Level = LogLevel.Warning,
        Message = "Update check failed: GitHub API response failed validation: {ErrorMessage}")]
    public static partial void UpdateResponseRejected(ILogger logger, Exception exception, string errorMessage);

    [LoggerMessage(
        EventId = 3007,
        EventName = nameof(UpdateResponseRejectedAsInvalidJson),
        Level = LogLevel.Warning,
        Message = "Update check failed: GitHub API response is not valid JSON")]
    public static partial void UpdateResponseRejectedAsInvalidJson(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3008,
        EventName = nameof(UpdateRequestTimedOut),
        Level = LogLevel.Warning,
        Message = "Update check failed: GitHub API request timed out")]
    public static partial void UpdateRequestTimedOut(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3009,
        EventName = nameof(UpdateResponseReadFailed),
        Level = LogLevel.Warning,
        Message = "Update check failed: reading the GitHub API response failed: {ErrorMessage}")]
    public static partial void UpdateResponseReadFailed(ILogger logger, Exception exception, string errorMessage);
}
