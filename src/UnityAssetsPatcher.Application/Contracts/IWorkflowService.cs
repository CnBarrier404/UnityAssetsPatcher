using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public interface IWorkflowService
{
    public OperationResult<RepositoryRecoveryReport> CheckPendingTransactions();

    public OperationResult<RepositoryRecoveryPreview> PreviewPendingTransaction(string gameDirectory);

    public OperationResult<RepositoryRecoveryReport> RecoverPendingTransactions(string gameDirectory);

    public OperationResult<InspectListResult> InspectList(InspectListRequest request);

    public OperationResult<AssetField> InspectFields(InspectFieldsRequest request);

    public OperationResult<IReadOnlyList<InstallRecordSummary>> ListInstalledMods();

    public OperationResult<UninstallPreviewResult> PreviewUninstall(UninstallPreviewRequest request);

    public OperationResult<UninstallModResult> Uninstall(UninstallModRequest request);
}
