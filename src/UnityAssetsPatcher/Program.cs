using UnityAssetsPatcher.AssetsTools;

namespace UnityAssetsPatcher;

public static class Program
{
    public static int Main(string[] args)
    {
        // The TPK is a bundled type database resource and does not depend on the startup working directory.
        // Source: https://github.com/AssetRipper/Tpk
        string tpkFilePath = Path.Combine(AppContext.BaseDirectory, "resources.tpk");

        var assetsTools = new AssetsToolsReader(tpkFilePath);
        string backupDirectory = Path.Combine(AppContext.BaseDirectory, "backup");
        var app = new ConsoleApp(assetsTools, assetsTools, backupDirectory, Console.Out, Console.Error);

        return app.Run(args);
    }
}
