using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Application.Backups;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class UninstallModWorkflow
{
    private readonly UninstallPlanner _planner;
    private readonly UninstallExecutor _executor;
    private readonly BackupRepository _backupRepository;

    public UninstallModWorkflow(UninstallPlanner planner, UninstallExecutor executor, BackupRepository backupRepository)
    {
        _planner = planner;
        _executor = executor;
        _backupRepository = backupRepository;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return _planner.ListInstalled();
    }

    public UninstallPreviewResult Preview(UninstallPreviewRequest request)
    {
        return _planner.BuildPreview(request);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        using BackupOperationLock operationLock = _backupRepository.AcquireLock();
        BackupRecoveryReport recovery = _backupRepository.CheckPendingTransactionsUnderLock();
        if (recovery.Status != BackupRepositoryStatus.Clean)
        {
            throw new BackupRecoveryException(
                recovery.Issues.FirstOrDefault()?.Message ??
                "A pending transaction must be recovered before uninstalling another mod.",
                recovery);
        }

        UninstallPlan plan = _planner.BuildUninstall(request);
        return _executor.Execute(plan) with { Recovery = recovery };
    }
}
