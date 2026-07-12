using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IAssetsAccessScopeFactory _assetsScopeFactory;
    private readonly WorkflowFactory _workflowFactory;

    internal WorkflowService(
        IAssetsAccessScopeFactory assetsScopeFactory,
        WorkflowFactory workflowFactory)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(workflowFactory);

        _assetsScopeFactory = assetsScopeFactory;
        _workflowFactory = workflowFactory;
    }

    public InstallPreviewResult PreviewInstall(InstallRequest request)
    {
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow workflow = _workflowFactory.CreateInstallWorkflow(assets);

        return workflow.Preview(request);
    }

    public InstallModResult Install(InstallRequest request)
    {
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow workflow = _workflowFactory.CreateInstallWorkflow(assets);

        return workflow.Install(request);
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow();

        return workflow.ListInstalled();
    }

    public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request)
    {
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow();

        return workflow.Preview(request);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow();

        return workflow.Uninstall(request);
    }
}
