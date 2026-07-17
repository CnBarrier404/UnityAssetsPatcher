using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public interface IWorkflowService
{
    public BackupRecoveryReport CheckPendingTransactions();
    public BackupRecoveryReport RecoverPendingTransactions();
    public ModManifest CheckManifest(string path);
    public InspectListResult InspectList(InspectListRequest request);
    public AssetsFieldInfo InspectFields(InspectFieldsRequest request);
    public InstallPreviewResult PreviewInstall(InstallRequest request);
    public InstallModResult Install(InstallRequest request);
    public IReadOnlyList<InstallRecordSummary> ListInstalledMods();
    public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request);
    public UninstallModResult Uninstall(UninstallModRequest request);
}
