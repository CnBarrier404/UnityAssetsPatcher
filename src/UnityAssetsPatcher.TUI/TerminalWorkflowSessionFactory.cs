using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.TUI;

internal sealed class TerminalWorkflowSessionFactory
{
    private readonly IAssetsAccessScopeFactory _assetsScopeFactory;
    private readonly WorkflowFactory _workflowFactory;

    public TerminalWorkflowSessionFactory(IAssetsAccessScopeFactory assetsScopeFactory)
    {
        _assetsScopeFactory = assetsScopeFactory;
        _workflowFactory = new WorkflowFactory();
    }

    public TerminalWorkflowSession CreateSession()
    {
        IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow installModWorkflow = _workflowFactory.CreateInstallModWorkflow(assets);
        InspectAssetsWorkflow inspectSummary_AssetsWorkflow = _workflowFactory.CreateInspectAssetsWorkflow(assets);
        FindAssetsWorkflow findSummary_AssetsWorkflow = _workflowFactory.CreateFindAssetsWorkflow(assets);

        return new TerminalWorkflowSession(
            installModWorkflow,
            inspectSummary_AssetsWorkflow,
            findSummary_AssetsWorkflow,
            assets);
    }
}
