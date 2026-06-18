using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.TUI;

public static class TerminalAppFactory
{
    public static TerminalApp CreateDefault(
        IWorkflowService workflowService,
        string backupDirectory,
        AppInfo appInfo)
    {
        ArgumentNullException.ThrowIfNull(workflowService);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(appInfo);

        IAnsiConsole errorConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Error),
        });

        return Create(
            workflowService,
            backupDirectory,
            appInfo,
            AnsiConsole.Console,
            errorConsole);
    }

    public static TerminalApp Create(
        IWorkflowService workflowService,
        string backupDirectory,
        IAnsiConsole console,
        IAnsiConsole errorConsole)
    {
        return Create(workflowService, backupDirectory, AppInfo.Default, console, errorConsole);
    }

    private static TerminalApp Create(
        IWorkflowService workflowService,
        string backupDirectory,
        AppInfo appInfo,
        IAnsiConsole console,
        IAnsiConsole errorConsole)
    {
        ArgumentNullException.ThrowIfNull(workflowService);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(errorConsole);

        return new TerminalApp(
            workflowService,
            backupDirectory,
            appInfo,
            console,
            errorConsole);
    }
}
