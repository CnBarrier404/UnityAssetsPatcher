using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Logging;

public static class LoggingService
{
    private const int RetainedFileCountLimit = 5;

    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    public static IServiceCollection AddUnityAssetsPatcherLogging(
        this IServiceCollection services,
        string logDirectory,
        LogLevel minimumLevel = LogLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        PruneOldLogFiles(logDirectory);

        var levelSwitch = new LoggingLevelSwitch(LevelConvert.ToSerilogLevel(minimumLevel));

        Serilog.ILogger logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(
                Path.Combine(logDirectory, $"log-{DateTime.Now:yyyyMMddHHmmss}.log"),
                outputTemplate: OutputTemplate)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSerilog(logger, true);
        });

        services.AddSingleton<ILoggingLevelSwitch>(new SerilogLoggingLevelSwitch(levelSwitch));

        return services;
    }

    private static void PruneOldLogFiles(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
        {
            return;
        }

        IEnumerable<string> expired = Directory.GetFiles(logDirectory, "log-*.log")
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
