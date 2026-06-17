using Spectre.Console;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.TUI;

public static class TerminalAppFactory
{
    public static TerminalApp CreateDefault(
        IAssetsAccessScopeFactory assetsScopeFactory,
        string backupDirectory)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(backupDirectory);

        IAnsiConsole errorConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Error),
        });

        return Create(
            assetsScopeFactory,
            backupDirectory,
            AnsiConsole.Console,
            errorConsole);
    }

    public static TerminalApp Create(
        IAssetsAccessScopeFactory assetsScopeFactory,
        string backupDirectory,
        IAnsiConsole console,
        IAnsiConsole errorConsole)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(errorConsole);

        return new TerminalApp(
            assetsScopeFactory,
            backupDirectory,
            console,
            errorConsole);
    }
}
