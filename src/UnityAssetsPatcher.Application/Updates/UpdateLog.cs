using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Application.Updates;

internal static partial class UpdateLog
{
    [LoggerMessage(
        EventId = 3004,
        EventName = nameof(UpdateRequestFailed),
        Level = LogLevel.Warning,
        Message = "Update check failed: {ErrorMessage}")]
    public static partial void UpdateRequestFailed(ILogger logger, Exception exception, string errorMessage);

    [LoggerMessage(
        EventId = 3005,
        EventName = nameof(UpdateRequestRejected),
        Level = LogLevel.Warning,
        Message = "Update check failed: the request returned HTTP status {StatusCode}.")]
    public static partial void UpdateRequestRejected(ILogger logger, Exception exception, int statusCode);

    [LoggerMessage(
        EventId = 3006,
        EventName = nameof(UpdateManifestRejected),
        Level = LogLevel.Warning,
        Message = "Update check failed: manifest was rejected: {ErrorMessage}")]
    public static partial void UpdateManifestRejected(ILogger logger, Exception exception, string errorMessage);

    [LoggerMessage(
        EventId = 3007,
        EventName = nameof(UpdateManifestRejectedAsInvalidJson),
        Level = LogLevel.Warning,
        Message = "Update check failed: manifest was rejected because it is not valid JSON.")]
    public static partial void UpdateManifestRejectedAsInvalidJson(ILogger logger, Exception exception);
}
