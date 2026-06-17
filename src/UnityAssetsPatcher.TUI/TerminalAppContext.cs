using Spectre.Console;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI;

internal sealed class TerminalAppContext
{
    public string BackupDirectory { get; }
    public IAnsiConsole Console { get; }
    public TerminalUI Ui { get; }
    public TerminalUI ErrorUi { get; }
    public TerminalPrompts Prompts { get; }
    public TerminalSettings Settings { get; }

    private readonly TerminalWorkflowSessionFactory _workflowSessionFactory;

    public TerminalAppContext(
        TerminalWorkflowSessionFactory workflowSessionFactory,
        string backupDirectory,
        AppInfo appInfo,
        IAnsiConsole console,
        IAnsiConsole error)
    {
        _workflowSessionFactory = workflowSessionFactory;
        BackupDirectory = backupDirectory;
        Console = console;
        Ui = new TerminalUI(console, appInfo);
        ErrorUi = new TerminalUI(error, appInfo);
        Prompts = new TerminalPrompts(console, Ui.Text);
        Settings = new TerminalSettings();
    }

    public void UseInstallWorkflow(Func<InstallModWorkflow, int> action)
    {
        using TerminalWorkflowSession session = _workflowSessionFactory.CreateSession();

        action.Invoke(session.InstallModWorkflow);
    }

    public void UseInspectWorkflow(Func<InspectAssetsWorkflow, int> action)
    {
        using TerminalWorkflowSession session = _workflowSessionFactory.CreateSession();

        action.Invoke(session.InspectAssetsWorkflow);
    }

    public void UseFindWorkflow(Func<FindAssetsWorkflow, int> action)
    {
        using TerminalWorkflowSession session = _workflowSessionFactory.CreateSession();

        action.Invoke(session.FindAssetsWorkflow);
    }
}
