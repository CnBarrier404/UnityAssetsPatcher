using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.TUI;

internal sealed class TerminalWorkflowSession : IDisposable
{
    private readonly IDisposable _disposable;

    public TerminalWorkflowSession(
        InstallModWorkflow installModWorkflow,
        InspectAssetsWorkflow inspectSummary_AssetsWorkflow,
        FindAssetsWorkflow findSummary_AssetsWorkflow,
        IDisposable disposable)
    {
        InstallModWorkflow = installModWorkflow;
        InspectAssetsWorkflow = inspectSummary_AssetsWorkflow;
        FindAssetsWorkflow = findSummary_AssetsWorkflow;
        _disposable = disposable;
    }

    public InstallModWorkflow InstallModWorkflow { get; }

    public InspectAssetsWorkflow InspectAssetsWorkflow { get; }

    public FindAssetsWorkflow FindAssetsWorkflow { get; }

    public void Dispose()
    {
        _disposable.Dispose();
    }
}
