using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Tui;

namespace UnityAssetsPatcher;

public static class Program
{
    public static int Main()
    {
        // The TPK is a bundled type database resource and does not depend on the startup working directory.
        // Source: https://github.com/AssetRipper/Tpk
        string tpkFilePath = Path.Combine(AppContext.BaseDirectory, "resources.tpk");

        var assetsFileWriter = new AssetsFileWriter(tpkFilePath);
        string backupDirectory = Path.Combine(AppContext.BaseDirectory, "backup");

        var app = TerminalApp.CreateDefault(
            () => new AssetsFileReader(tpkFilePath),
            assetsFileWriter,
            backupDirectory);

        return app.Run();
    }
}
