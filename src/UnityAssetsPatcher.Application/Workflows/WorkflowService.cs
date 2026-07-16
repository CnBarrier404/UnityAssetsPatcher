using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IAssetsAccessScopeFactory _assetsScopeFactory;
    private readonly WorkflowFactory _workflowFactory;
    private readonly ModManifestReader _manifestReader;
    private readonly ModBackupStore _backupStore;

    internal WorkflowService(
        IAssetsAccessScopeFactory assetsScopeFactory,
        WorkflowFactory workflowFactory,
        ModManifestReader manifestReader,
        ModBackupStore backupStore)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(workflowFactory);
        ArgumentNullException.ThrowIfNull(manifestReader);
        ArgumentNullException.ThrowIfNull(backupStore);

        _assetsScopeFactory = assetsScopeFactory;
        _workflowFactory = workflowFactory;
        _manifestReader = manifestReader;
        _backupStore = backupStore;
    }

    public void RecoverPendingTransactions() => _backupStore.RecoverPendingTransactions();

    public ModManifest CheckManifest(string path) => _manifestReader.Load(path);

    public InspectListResult InspectList(InspectListRequest request)
    {
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InspectAssetsWorkflow workflow = _workflowFactory.CreateInspectWorkflow(assets);

        return workflow.List(request);
    }

    public AssetsFieldInfo InspectFields(InspectFieldsRequest request)
    {
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InspectAssetsWorkflow workflow = _workflowFactory.CreateInspectWorkflow(assets);

        return workflow.Fields(request);
    }

    public InstallPreviewResult PreviewInstall(InstallRequest request)
    {
        RecoverPendingTransactions();
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow workflow = _workflowFactory.CreateInstallWorkflow(assets);

        return workflow.Preview(request);
    }

    public InstallModResult Install(InstallRequest request)
    {
        RecoverPendingTransactions();
        using IAssetsAccessScope assets = _assetsScopeFactory.CreateScope();
        InstallModWorkflow workflow = _workflowFactory.CreateInstallWorkflow(assets);

        return workflow.Install(request);
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        RecoverPendingTransactions();
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow();

        return workflow.ListInstalled();
    }

    public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request)
    {
        RecoverPendingTransactions();
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow();

        return workflow.Preview(request);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        RecoverPendingTransactions();
        UninstallModWorkflow workflow = _workflowFactory.CreateUninstallWorkflow();

        return workflow.Uninstall(request);
    }
}
