using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IAssetsAccessScopeFactory _assetsScopeFactory;
    private readonly string _backupDirectory;
    private readonly IInstallModWorkflowFactory _installWorkflowFactory;
    private readonly IUninstallModWorkflowFactory _uninstallWorkflowFactory;

    public WorkflowService(
        IAssetsAccessScopeFactory assetsScopeFactory,
        string backupDirectory,
        IInstallModWorkflowFactory installWorkflowFactory,
        IUninstallModWorkflowFactory uninstallWorkflowFactory)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(installWorkflowFactory);
        ArgumentNullException.ThrowIfNull(uninstallWorkflowFactory);

        _assetsScopeFactory = assetsScopeFactory;
        _backupDirectory = backupDirectory;
        _installWorkflowFactory = installWorkflowFactory;
        _uninstallWorkflowFactory = uninstallWorkflowFactory;
    }

    public InstallPreviewResult PreviewInstall(InstallPreviewRequest request)
    {
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow workflow = _installWorkflowFactory.Create(assets);

        return workflow.Preview(request);
    }

    public InstallModResult Install(InstallModRequest request)
    {
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow workflow = _installWorkflowFactory.Create(assets);

        return workflow.Install(request);
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        UninstallModWorkflow workflow = _uninstallWorkflowFactory.Create(_backupDirectory);

        return workflow.ListInstalled();
    }

    public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request)
    {
        UninstallModWorkflow workflow = _uninstallWorkflowFactory.Create(_backupDirectory);

        return workflow.Preview(request);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        UninstallModWorkflow workflow = _uninstallWorkflowFactory.Create(_backupDirectory);

        return workflow.Uninstall(request);
    }
}
