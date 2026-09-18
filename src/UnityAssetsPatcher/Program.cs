using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.ViewModels;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.Logging;

namespace UnityAssetsPatcher;

public sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        return RunAsync(args).GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(string[] args)
    {
        Logger rootLogger;
        LoggingLevelSwitch loggingLevelSwitch;

        try
        {
            rootLogger = LoggingService.CreateUnityAssetsPatcherLogger(out loggingLevelSwitch);
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync("An unexpected error occurred.");

            return 1;
        }

        await using (rootLogger)
        {
            ServiceProvider? serviceProvider = null;
            Exception? unexpectedException = null;
            int exitCode = 1;

            try
            {
                serviceProvider = new ServiceCollection()
                    .AddUnityAssetsPatcherLogging(rootLogger, loggingLevelSwitch)
                    .AddUnityAssetsPatcherGitHubUpdates()
                    .AddUnityAssetsPatcherInfrastructure(OpenClassPackage)
                    .AddUnityAssetsPatcherRepository()
                    .AddUnityAssetsPatcherApplication()
                    .AddUnityAssetsPatcherUpdates()
                    .AddUnityAssetsPatcherOperations()
                    .AddSingleton<MainWindowViewModel>()
                    .AddSingleton<App>()
                    .BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });

                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("Application started");

                exitCode = AppBuilder.Configure(() => serviceProvider.GetRequiredService<App>())
                    .UsePlatformDetect()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception exception)
            {
                unexpectedException = exception;
            }

            if (serviceProvider is not null)
            {
                try
                {
                    await serviceProvider.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception disposalException)
                {
                    unexpectedException = unexpectedException is null
                        ? disposalException
                        : new AggregateException(
                            "Application execution and service provider disposal both failed.",
                            unexpectedException,
                            disposalException);
                }
            }

            if (unexpectedException is null)
            {
                return exitCode;
            }

            rootLogger
                .ForContext<Program>()
                .Fatal(unexpectedException, "Application terminated unexpectedly");

            await Console.Error.WriteLineAsync("An unexpected error occurred.");

            return 1;
        }

        Stream OpenClassPackage()
        {
            return typeof(Program).Assembly.GetManifestResourceStream("resources.tpk") ??
                   throw new InvalidOperationException("The bundled AssetsTools class package is missing.");
        }
    }
}
