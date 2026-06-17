using Spectre.Console;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.TUI;

public static class TerminalAppFactory
{
    public static TerminalApp CreateDefault(
        IAssetsAccessScopeFactory assetsScopeFactory,
        string backupDirectory,
        AppInfo appInfo)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(appInfo);

        IAnsiConsole errorConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Error),
        });

        return Create(
            assetsScopeFactory,
            backupDirectory,
            appInfo,
            AnsiConsole.Console,
            errorConsole);
    }

    public static TerminalApp Create(
        IAssetsAccessScopeFactory assetsScopeFactory,
        string backupDirectory,
        IAnsiConsole console,
        IAnsiConsole errorConsole)
    {
        return Create(assetsScopeFactory, backupDirectory, AppInfo.Default, console, errorConsole);
    }

    private static TerminalApp Create(
        IAssetsAccessScopeFactory assetsScopeFactory,
        string backupDirectory,
        AppInfo appInfo,
        IAnsiConsole console,
        IAnsiConsole errorConsole)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(errorConsole);

        return new TerminalApp(
            assetsScopeFactory,
            backupDirectory,
            appInfo,
            console,
            errorConsole);
    }
}
