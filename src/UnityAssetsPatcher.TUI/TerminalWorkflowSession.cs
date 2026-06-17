using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.TUI;

internal sealed class TerminalWorkflowSession : IDisposable
{
    public InstallModWorkflow InstallModWorkflow { get; }

    private readonly IDisposable _disposable;

    public TerminalWorkflowSession(InstallModWorkflow installModWorkflow, IDisposable disposable)
    {
        InstallModWorkflow = installModWorkflow;
        _disposable = disposable;
    }

    public void Dispose()
    {
        _disposable.Dispose();
    }
}
