using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Contracts;

public interface IWorkflowService
{
    public OperationResult<RepositoryRecoveryReport> CheckPendingTransactions();

    public OperationResult<RepositoryRecoveryPreview> PreviewPendingTransaction(string gameDirectory);

    public OperationResult<RepositoryRecoveryReport> RecoverPendingTransactions(string gameDirectory);

    public OperationResult<IReadOnlyList<InstallRecordSummary>> ListInstalledMods();
}
