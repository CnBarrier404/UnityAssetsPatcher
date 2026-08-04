using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public interface IWorkflowService
{
    public OperationResult<BackupRecoveryReport> CheckPendingTransactions();

    public OperationResult<BackupRecoveryPreview> PreviewPendingTransaction(string gameDirectory);

    public OperationResult<BackupRecoveryReport> RecoverPendingTransactions(string gameDirectory);

    public OperationResult<InspectListResult> InspectList(InspectListRequest request);

    public OperationResult<AssetField> InspectFields(InspectFieldsRequest request);

    public OperationResult<InstallPreviewResult> PreviewInstall(InstallRequest request);

    public OperationResult<InstallModResult> Install(InstallRequest request);

    public OperationResult<IReadOnlyList<InstallRecordSummary>> ListInstalledMods();

    public OperationResult<UninstallPreviewResult> PreviewUninstall(UninstallPreviewRequest request);

    public OperationResult<UninstallModResult> Uninstall(UninstallModRequest request);
}
