using Spectre.Console;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui;

public static class TerminalAppFactory
{
    public static TerminalApp CreateDefault(
        Func<IAssetsFileReader> createAssetsReader,
        IAssetsFileWriter assetsPatchWriter,
        string backupDirectory)
    {
        ArgumentNullException.ThrowIfNull(createAssetsReader);
        ArgumentNullException.ThrowIfNull(assetsPatchWriter);
        ArgumentNullException.ThrowIfNull(backupDirectory);

        IAnsiConsole errorConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Error),
        });

        return Create(
            createAssetsReader,
            assetsPatchWriter,
            backupDirectory,
            AnsiConsole.Console,
            errorConsole);
    }

    public static TerminalApp Create(
        Func<IAssetsFileReader> createAssetsReader,
        IAssetsFileWriter assetsPatchWriter,
        string backupDirectory,
        IAnsiConsole console,
        IAnsiConsole errorConsole)
    {
        ArgumentNullException.ThrowIfNull(createAssetsReader);
        ArgumentNullException.ThrowIfNull(assetsPatchWriter);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(errorConsole);

        return new TerminalApp(
            createAssetsReader,
            assetsPatchWriter,
            console,
            errorConsole,
            backupDirectory);
    }
}
