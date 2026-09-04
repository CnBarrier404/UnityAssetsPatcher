using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.CLI;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.Logging;
using UnityAssetsPatcher.TUI;

namespace UnityAssetsPatcher;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
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
                    .AddUnityAssetsPatcherCli()
                    .AddUnityAssetsPatcherOperationalCommands()
                    .AddUnityAssetsPatcherTUI()
                    .BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });

                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("Application started.");

                if (args.Length > 0)
                {
                    var cliApplication = serviceProvider.GetRequiredService<CLIApplication>();

                    exitCode = await cliApplication.RunAsync(args).ConfigureAwait(false);
                }
                else
                {
                    var terminalApp = serviceProvider.GetRequiredService<TerminalApp>();

                    exitCode = await terminalApp.RunAsync().ConfigureAwait(false);
                }
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
                .Fatal(unexpectedException, "Application terminated unexpectedly.");

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
