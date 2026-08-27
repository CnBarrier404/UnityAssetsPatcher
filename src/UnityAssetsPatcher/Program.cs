using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.CLI;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.TUI;
using UnityAssetsPatcher.Logging;

namespace UnityAssetsPatcher;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        await using ServiceProvider serviceProvider = new ServiceCollection()
            .AddUnityAssetsPatcherLogging()
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

        logger.LogInformation("Application started");

        if (args.Length > 0)
        {
            var cliApplication = serviceProvider.GetRequiredService<CLIApplication>();

            return await cliApplication.RunAsync(args).ConfigureAwait(false);
        }

        var terminalApp = serviceProvider.GetRequiredService<TerminalApp>();

        return await terminalApp.RunAsync().ConfigureAwait(false);

        Stream OpenClassPackage()
        {
            return typeof(Program).Assembly.GetManifestResourceStream("resources.tpk") ??
                   throw new InvalidOperationException(
                       "The bundled AssetsTools class package is missing.");
        }
    }
}
