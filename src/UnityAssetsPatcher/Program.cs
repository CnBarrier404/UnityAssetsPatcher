using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.Logging;
using UnityAssetsPatcher.TUI;
using UnityAssetsPatcher.CLI;

namespace UnityAssetsPatcher;

public sealed class Program
{
    private const string TpkResourceName = "resources.tpk";

    public static int Main(string[] args)
    {
        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnityAssetsPatcher");
        string backupDirectory = Path.Combine(appDataDirectory, "backup");
        string logDirectory = Path.Combine(appDataDirectory, "logs");

        AppInfo appInfo = AppInfo.FromAssembly("Unity Assets Patcher", typeof(Program).Assembly);

        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddUnityAssetsPatcherLogging(logDirectory)
            .AddUnityAssetsPatcherInfrastructure()
            .AddUnityAssetsPatcherAssetsTools(OpenTpkResource)
            .AddUnityAssetsPatcherApplication(backupDirectory)
            .AddUnityAssetsPatcherCLI()
            .AddUnityAssetsPatcherTUI(appInfo)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Application started");

        return args.Length > 0
            ? serviceProvider.GetRequiredService<CLIApplication>().Run(args)
            : serviceProvider.GetRequiredService<TerminalApp>().Run();

        // TPKSource: https://github.com/AssetRipper/Tpk
        Stream OpenTpkResource() => typeof(Program).Assembly.GetManifestResourceStream(TpkResourceName)
                                    ?? throw new InvalidOperationException(
                                        $"Embedded TPK resource not found: {TpkResourceName}");
    }
}
