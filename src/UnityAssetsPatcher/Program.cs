using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI;
using UnityAssetsPatcher.CLI;

namespace UnityAssetsPatcher;

public static class Program
{
    private const string TpkResourceName = "resources.tpk";

    public static int Main(string[] args)
    {
        string backupDirectory = Path.Combine(AppContext.BaseDirectory, "backup");
        AppInfo appInfo = AppInfo.FromAssembly("Unity Assets Patcher", typeof(Program).Assembly);

        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddUnityAssetsPatcherAssetsTools(OpenTpkResource)
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

        serviceProvider.GetRequiredService<IWorkflowService>().RecoverPendingTransactions();

        return serviceProvider.GetRequiredService<TerminalApp>().Run();

        // TPKSource: https://github.com/AssetRipper/Tpk
        Stream OpenTpkResource() => typeof(Program).Assembly.GetManifestResourceStream(TpkResourceName)
                                    ?? throw new InvalidOperationException(
                                        $"Embedded TPK resource not found: {TpkResourceName}");
    }
}
