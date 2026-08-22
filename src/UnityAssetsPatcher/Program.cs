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
        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityAssetsPatcher");

        string logDirectory = Path.Combine(appDataDirectory, "logs");
        string repositoryDirectory = Path.Combine(appDataDirectory, "backup");
        AppInfo appInfo = AppInfo.FromAssembly("Unity Assets Patcher", typeof(Program).Assembly);
        var openClassPackage = () => typeof(Program).Assembly.GetManifestResourceStream("resources.tpk") ??
                                     throw new InvalidOperationException(
                                         "The bundled AssetsTools class package is missing.");

        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton(appInfo)
            .AddUnityAssetsPatcherLogging(logDirectory)
            .AddUnityAssetsPatcherGitHubUpdates()
            .AddUnityAssetsPatcherInfrastructure(openClassPackage)
            .AddUnityAssetsPatcherRepository(repositoryDirectory)
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

        return terminalApp.Run();
    }
}
