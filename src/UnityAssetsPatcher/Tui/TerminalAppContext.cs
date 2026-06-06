using Spectre.Console;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalAppContext
{
    public string BackupDirectory { get; }
    public IAnsiConsole Console { get; }
    public IAnsiConsole Error { get; }
    public TerminalPageLayout Layout { get; }
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
        Error = error;
        Layout = new TerminalPageLayout(console);
        Settings = new TerminalSettings();
    }

    public void UseService(Func<AssetsWorkflowService, int> action)
    {
        using TerminalWorkflowSession session = _workflowSessionFactory.CreateSession();

        action.Invoke(session.Service);
    }
}
