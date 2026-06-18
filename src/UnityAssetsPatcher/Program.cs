using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI;

namespace UnityAssetsPatcher;

public static class Program
{
    public static int Main()
    {
        // The TPK is a bundled type database resource and does not depend on the startup working directory.
        // Source: https://github.com/AssetRipper/Tpk
        string tpkFilePath = Path.Combine(AppContext.BaseDirectory, "resources.tpk");
        string backupDirectory = Path.Combine(AppContext.BaseDirectory, "backup");
        AppInfo appInfo = AppInfo.FromAssembly("Unity Assets Patcher", typeof(Program).Assembly);

        using var assetsScopeFactory = new AssetsToolsAccessScopeFactory(tpkFilePath);
        var workflowFactory = new WorkflowFactory();
        IWorkflowService workflowService = new WorkflowService(assetsScopeFactory, backupDirectory, workflowFactory);

        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton(workflowService)
            .AddUnityAssetsPatcherTUI(
                backupDirectory,
                appInfo,
                Spectre.Console.AnsiConsole.Console)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        var app = serviceProvider.GetRequiredService<TerminalApp>();

        return app.Run();
    }
}
