using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Logging;

public static class LoggingService
{
    private const int RetainedFileCountLimit = 5;

    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    public static Logger CreateUnityAssetsPatcherLogger(
        out LoggingLevelSwitch levelSwitch,
        string? logDirectory = null,
        LoggingLevel minimumLevel = LoggingLevel.Information)
    {
        logDirectory ??= AppConfig.LogDirectory;
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        PruneOldLogFiles(logDirectory);

        levelSwitch = new LoggingLevelSwitch(ToSerilogLevel(minimumLevel));

        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(
                Path.Combine(logDirectory, $"log-{DateTime.Now:yyyyMMddHHmmss}.log"),
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }

    public static IServiceCollection AddUnityAssetsPatcherLogging(
        this IServiceCollection services,
        Serilog.ILogger logger,
        LoggingLevelSwitch levelSwitch)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(levelSwitch);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSerilog(logger, false);
        });

        services.AddSingleton<ILoggingLevelSwitch>(new SerilogLoggingLevelSwitch(levelSwitch));

        return services;
    }

    private static LogEventLevel ToSerilogLevel(LoggingLevel level)
    {
        return level switch
        {
            LoggingLevel.Information => LogEventLevel.Information,
            LoggingLevel.Debug => LogEventLevel.Debug,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported logging level.")
        };
    }

    private static void PruneOldLogFiles(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
        {
            return;
        }

        var expired = Directory.GetFiles(logDirectory, "log-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(RetainedFileCountLimit - 1);

        foreach (string path in expired)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
