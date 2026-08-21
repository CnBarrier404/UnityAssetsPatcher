using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Infrastructure.Updates;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Updates;

public sealed class UpdateLogTests
{
    [Fact]
    public void NormalUpdateEventsUseInformationLevel()
    {
        var logger = new RecordingLogger();

        UpdateLog.UpdateCheckStarted(logger);
        UpdateLog.UpdateCheckCompletedWithoutUpdate(logger, "v1.2.3", "v1.2.3");
        UpdateLog.UpdateCheckCompletedWithUpdate(logger, "v1.2.3", "v1.3.0");
        UpdateLog.UpdateCheckSkipped(logger);

        Assert.Collection(
            logger.Records,
            record => AssertLog(
                record,
                LogLevel.Information,
                3001,
                nameof(UpdateLog.UpdateCheckStarted),
                "Checking for updates..."),
            record => AssertLog(
                record,
                LogLevel.Information,
                3002,
                nameof(UpdateLog.UpdateCheckCompletedWithoutUpdate),
                "Update check completed: no update available (current version {CurrentVersion}, latest version {LatestVersion})."),
            record => AssertLog(
                record,
                LogLevel.Information,
                3003,
                nameof(UpdateLog.UpdateCheckCompletedWithUpdate),
                "Update check completed: new version {LatestVersion} is available (current version {CurrentVersion})."),
            record => AssertLog(
                record,
                LogLevel.Information,
                3000,
                nameof(UpdateLog.UpdateCheckSkipped),
                "Skipping update check because current version is a development version."));
    }

    [Fact]
    public void FailureEventsUseWarningLevelAndCancellationUsesDebugLevel()
    {
        var logger = new RecordingLogger();
        var requestException = new IOException("Offline");
        var jsonException = new System.Text.Json.JsonException("Invalid JSON");

        UpdateLog.UpdateRequestFailed(logger, requestException, requestException.Message);
        UpdateLog.UpdateRequestRejected(logger, 503);
        UpdateLog.UpdateManifestRejected(logger);
        UpdateLog.UpdateManifestRejectedAsInvalidJson(logger, jsonException);
        UpdateLog.UpdateManifestRejectedAsTooLarge(logger, 64 * 1024);
        UpdateLog.UpdateCheckCanceled(logger);

        Assert.Collection(
            logger.Records,
            record =>
            {
                AssertLog(
                    record,
                    LogLevel.Warning,
                    3004,
                    nameof(UpdateLog.UpdateRequestFailed),
                    "Update check failed: {ErrorMessage}.");
                Assert.Same(requestException, record.Exception);
                Assert.Equal(requestException.Message, record.Properties["ErrorMessage"]);
            },
            record => AssertLog(
                record,
                LogLevel.Warning,
                3005,
                nameof(UpdateLog.UpdateRequestRejected),
                "Update check failed: the request returned HTTP status {StatusCode}."),
            record =>
            {
                AssertLog(
                    record,
                    LogLevel.Warning,
                    3006,
                    nameof(UpdateLog.UpdateManifestRejected),
                    "Update check failed: manifest was rejected because it does not match the expected format.");
            },
            record =>
            {
                AssertLog(
                    record,
                    LogLevel.Warning,
                    3007,
                    nameof(UpdateLog.UpdateManifestRejectedAsInvalidJson),
                    "Update check failed: manifest was rejected because it is not valid JSON.");
                Assert.Same(jsonException, record.Exception);
            },
            record =>
            {
                AssertLog(
                    record,
                    LogLevel.Warning,
                    3008,
                    nameof(UpdateLog.UpdateManifestRejectedAsTooLarge),
                    "Update check failed: manifest was rejected because it exceeds the maximum size of {MaximumSize} bytes.");
            },
            record => AssertLog(
                record,
                LogLevel.Debug,
                3009,
                nameof(UpdateLog.UpdateCheckCanceled),
                "Update check was canceled."));
    }

    private static void AssertLog(
        LogRecord record,
        LogLevel level,
        int eventId,
        string eventName,
        string message)
    {
        Assert.Equal(level, record.Level);
        Assert.Equal(eventId, record.EventId.Id);
        Assert.Equal(eventName, record.EventId.Name);
        Assert.Equal(message, record.Properties["{OriginalFormat}"]);
    }

    private sealed record LogRecord(
        LogLevel Level,
        EventId EventId,
        IReadOnlyDictionary<string, object?> Properties,
        Exception? Exception);

    private sealed class RecordingLogger : ILogger
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            IReadOnlyDictionary<string, object?> properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            Records.Add(new LogRecord(logLevel, eventId, properties, exception));
        }
    }
}
