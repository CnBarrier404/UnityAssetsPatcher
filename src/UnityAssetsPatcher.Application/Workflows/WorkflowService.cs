using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IAssetsAccessScopeFactory _assetsScopeFactory;
    private readonly string _backupDirectory;
    private readonly WorkflowFactory _workflowFactory;

    internal WorkflowService(
        IAssetsAccessScopeFactory assetsScopeFactory,
        string backupDirectory,
        WorkflowFactory workflowFactory)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(workflowFactory);

        _assetsScopeFactory = assetsScopeFactory;
        _backupDirectory = backupDirectory;
        _workflowFactory = workflowFactory;
    }

    public InstallPreviewResult PreviewInstall(InstallPreviewRequest request)
    {
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow workflow = _workflowFactory.CreateInstallWorkflow(assets);

        return workflow.Preview(request);
    }

    public InstallModResult Install(InstallModRequest request)
    {
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow workflow = _workflowFactory.CreateInstallWorkflow(assets);

        return workflow.Install(request);
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow(_backupDirectory);

        return workflow.ListInstalled();
    }

    public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request)
    {
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow(_backupDirectory);

        return workflow.Preview(request);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow(_backupDirectory);

        return workflow.Uninstall(request);
    }
}
