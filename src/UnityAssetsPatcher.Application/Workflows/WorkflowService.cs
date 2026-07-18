using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IAssetsAccessScopeFactory _assetsScopeFactory;
    private readonly WorkflowFactory _workflowFactory;
    private readonly ModManifestReader _manifestReader;
    private readonly BackupRepository _backupRepository;

    internal WorkflowService(
        IAssetsAccessScopeFactory assetsScopeFactory,
        WorkflowFactory workflowFactory,
        ModManifestReader manifestReader,
        BackupRepository backupRepository)
    {
        ArgumentNullException.ThrowIfNull(assetsScopeFactory);
        ArgumentNullException.ThrowIfNull(workflowFactory);
        ArgumentNullException.ThrowIfNull(manifestReader);
        ArgumentNullException.ThrowIfNull(backupRepository);

        _assetsScopeFactory = assetsScopeFactory;
        _workflowFactory = workflowFactory;
        _manifestReader = manifestReader;
        _backupRepository = backupRepository;
    }

    public BackupRecoveryPreview PreviewPendingTransaction(string gameDirectory)
    {
        return _backupRepository.PreviewPendingTransaction(gameDirectory);
    }

    public BackupRecoveryReport RecoverPendingTransactions(string gameDirectory)
    {
        return _backupRepository.RecoverPendingTransactions(gameDirectory);
    }

    public BackupRecoveryReport CheckPendingTransactions()
    {
        return _backupRepository.CheckPendingTransactions();
    }

    public ModManifest CheckManifest(string path)
    {
        return _manifestReader.Load(path);
    }

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
