using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.CLI;

namespace UnityAssetsPatcher;

public static class Program
{
    public static int Main(string[] args)
    {
        // The TPK is a bundled type database resource and does not depend on the startup working directory.
        // Source: https://github.com/AssetRipper/Tpk
        string tpkFilePath = Path.Combine(AppContext.BaseDirectory, "resources.tpk");
        string backupDirectory = Path.Combine(AppContext.BaseDirectory, "backup");
        AppInfo appInfo = AppInfo.FromAssembly("Unity Assets Patcher", typeof(Program).Assembly);

        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddUnityAssetsPatcherAssetsTools(tpkFilePath)
            .AddUnityAssetsPatcherApplication(backupDirectory)
            .AddUnityAssetsPatcherCLI()
            .AddUnityAssetsPatcherTUI(appInfo)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        if (args.Length > 0)
        {
            return serviceProvider.GetRequiredService<CLIApplication>().Run(args);
        }

        serviceProvider.GetRequiredService<ModBackupStore>().RecoverPendingTransactions();
        return serviceProvider.GetRequiredService<TerminalApp>().Run();
    }
}
