using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Logging;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class LoggingServiceTests
{
    [Fact]
    public void AddUnityAssetsPatcherLogging_WhenInformationIsLogged_WritesToRollingFile()
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
            Assert.Matches(@"^log-\d{8}\.log$", Path.GetFileName(logFile));

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
    public void AddUnityAssetsPatcherLogging_WhenCalledTwiceSameDay_AppendsToSameFile()
    {
        string logDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            WriteLogEntry(logDirectory, "First run");
            WriteLogEntry(logDirectory, "Second run");

            string logFile = Assert.Single(Directory.GetFiles(logDirectory, "*.log"));
            string logContents = File.ReadAllText(logFile);

            Assert.Contains("First run", logContents);
            Assert.Contains("Second run", logContents);
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
