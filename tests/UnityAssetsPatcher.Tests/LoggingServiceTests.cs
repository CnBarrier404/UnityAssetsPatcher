using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Logging;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class LoggingServiceTests
{
    [Fact]
    public void AddUnityAssetsPatcherLogging_WhenInformationIsLogged_WritesToTimestampedFile()
    {
        string logDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            using (ServiceProvider provider = new ServiceCollection()
                       .AddUnityAssetsPatcherLogging(logDirectory)
                       .BuildServiceProvider())
            {
                ILogger<LoggingServiceTests> logger =
                    provider.GetRequiredService<ILogger<LoggingServiceTests>>();

                logger.LogInformation("Test log entry {Value}", 42);
            }

            string logFile = Assert.Single(Directory.GetFiles(logDirectory, "*.log"));
            Assert.Matches(@"^log-\d{14}\.log$", Path.GetFileName(logFile));

            string logContents = File.ReadAllText(logFile);
            Assert.Contains("Test log entry 42", logContents);
            Assert.Contains(typeof(LoggingServiceTests).FullName!, logContents);
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                Directory.Delete(logDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void AddUnityAssetsPatcherLogging_WhenFiveLogFilesExist_DeletesOldestBeforeCreatingNewFile()
    {
        string logDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(logDirectory);

            for (int index = 0; index < 5; index++)
            {
                string oldFile = Path.Combine(logDirectory, $"log-2026010100000{index}.log");
                File.WriteAllText(oldFile, $"old run {index}");
                File.SetLastWriteTimeUtc(oldFile, new DateTime(2026, 1, 1, 0, 0, index, DateTimeKind.Utc));
            }

            WriteLogEntry(logDirectory, "New run");

            string[] logFiles = Directory.GetFiles(logDirectory, "log-*.log");

            Assert.Equal(5, logFiles.Length);
            Assert.DoesNotContain(logFiles, file => Path.GetFileName(file) == "log-20260101000000.log");

            string newestFile = logFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
            Assert.Contains("New run", File.ReadAllText(newestFile));
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                Directory.Delete(logDirectory, recursive: true);
            }
        }
    }

    private static void WriteLogEntry(string logDirectory, string message)
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddUnityAssetsPatcherLogging(logDirectory)
            .BuildServiceProvider();
        ILogger<LoggingServiceTests> logger =
            provider.GetRequiredService<ILogger<LoggingServiceTests>>();

        logger.LogInformation("{Message}", message);
    }
}
