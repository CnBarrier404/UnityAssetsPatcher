using Spectre.Console;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalAppContext
{
    public string BackupDirectory { get; }
    public IAnsiConsole Console { get; }
    public TerminalRenderer Renderer { get; }
    public TerminalRenderer ErrorRenderer { get; }
    public TerminalSettings Settings { get; }

    private readonly ITerminalWorkflowSessionFactory _workflowSessionFactory;

    public TerminalAppContext(
        ITerminalWorkflowSessionFactory workflowSessionFactory,
        string backupDirectory,
        IAnsiConsole console,
        IAnsiConsole error)
    {
        _workflowSessionFactory = workflowSessionFactory;
        BackupDirectory = backupDirectory;
        Console = console;
        Renderer = new TerminalRenderer(console);
        ErrorRenderer = new TerminalRenderer(error);
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
