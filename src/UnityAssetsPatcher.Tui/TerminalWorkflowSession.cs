using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalWorkflowSession : IDisposable
{
    private readonly IDisposable? _disposable;

    public TerminalWorkflowSession(
        InstallModWorkflow installModWorkflow,
        InspectAssetsWorkflow inspectAssetsWorkflow,
        FindAssetsWorkflow findAssetsWorkflow,
        IDisposable? disposable)
    {
        InstallModWorkflow = installModWorkflow;
        InspectAssetsWorkflow = inspectAssetsWorkflow;
        FindAssetsWorkflow = findAssetsWorkflow;
        _disposable = disposable;
    }

    public InstallModWorkflow InstallModWorkflow { get; }

    public InspectAssetsWorkflow InspectAssetsWorkflow { get; }

    public FindAssetsWorkflow FindAssetsWorkflow { get; }

    public void Dispose()
    {
        _disposable?.Dispose();
    }
}
