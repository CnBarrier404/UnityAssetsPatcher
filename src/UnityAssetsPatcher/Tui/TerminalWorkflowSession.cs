using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalWorkflowSession : IDisposable
{
    private readonly IDisposable? _disposable;

    public TerminalWorkflowSession(InstallModWorkflow installModWorkflow, IDisposable? disposable)
    {
        InstallModWorkflow = installModWorkflow;
        _disposable = disposable;
    }

    public InstallModWorkflow InstallModWorkflow { get; }

    public void Dispose()
    {
        _disposable?.Dispose();
    }
}
