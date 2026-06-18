using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI;

internal sealed class TerminalAppContext
{
    public IWorkflowService WorkflowService { get; }
    public string BackupDirectory { get; }
    public IAnsiConsole Console { get; }
    public TerminalUI Ui { get; }
    public TerminalUI ErrorUi { get; }
    public TerminalPrompts Prompts { get; }
    public TerminalSettings Settings { get; }

    public TerminalAppContext(
        IWorkflowService workflowService,
        string backupDirectory,
        AppInfo appInfo,
        IAnsiConsole console,
        IAnsiConsole error)
    {
        WorkflowService = workflowService;
        BackupDirectory = backupDirectory;
        Console = console;
        Ui = new TerminalUI(console, appInfo);
        ErrorUi = new TerminalUI(error, appInfo);
        Prompts = new TerminalPrompts(console, Ui.Text);
        Settings = new TerminalSettings();
    }
}
