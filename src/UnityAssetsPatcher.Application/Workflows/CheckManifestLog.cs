using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Application.Workflows;

internal static partial class CheckManifestLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "OperationStarted: checking mod source {ManifestPath}")]
    public static partial void OperationStarted(ILogger logger, string? manifestPath);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "OperationSucceeded: checked mod {ModName} version {ModVersion} in {ElapsedMilliseconds} ms")]
    public static partial void OperationSucceeded(
        ILogger logger,
        string modName,
        string modVersion,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "OperationFailed: check failed with {ErrorCode} in {ElapsedMilliseconds} ms")]
    public static partial void OperationFailed(ILogger logger, string errorCode, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "OperationFaulted: check faulted in {ElapsedMilliseconds} ms")]
    public static partial void OperationFaulted(ILogger logger, double elapsedMilliseconds, Exception exception);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Check operation canceled in {ElapsedMilliseconds} ms")]
    public static partial void OperationCanceled(ILogger logger, double elapsedMilliseconds);
}
