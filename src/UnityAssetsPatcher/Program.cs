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

        TerminalApp app = TerminalAppFactory.CreateDefault(
            assetsScopeFactory,
            backupDirectory,
            appInfo);

        return app.Run();
    }
}
