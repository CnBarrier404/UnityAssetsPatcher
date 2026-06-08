using Spectre.Console;
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

        var assetsFileService = new AssetsFileService(tpkFilePath);
        string backupDirectory = Path.Combine(AppContext.BaseDirectory, "backup");

        IAnsiConsole errorConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Error),
        });

        var app = new TerminalApp(
            () => new AssetsFileReader(tpkFilePath),
            assetsFileService,
            backupDirectory,
            AnsiConsole.Console,
            errorConsole);

        return app.Run();
    }
}
