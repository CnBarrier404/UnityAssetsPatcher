using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Application.Backups;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class UninstallModWorkflow
{
    private readonly UninstallPlanner _planner;
    private readonly UninstallExecutor _executor;
    private readonly ModBackupStore _backupStore;

    public UninstallModWorkflow(UninstallPlanner planner, UninstallExecutor executor, ModBackupStore backupStore)
    {
        _planner = planner;
        _executor = executor;
        _backupStore = backupStore;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return _planner.ListInstalled();
    }

    public UninstallPreviewResult Preview(UninstallPreviewRequest request)
    {
        UninstallPreviewPlan plan = _planner.BuildPreview(request);

        return UninstallResultMapper.ToPreviewResult(plan);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        using BackupOperationLock operationLock = _backupStore.AcquireOperationLock();
        UninstallPlan plan = _planner.BuildUninstall(request);
        UninstallExecutionResult execution = _executor.Execute(plan);

        return UninstallResultMapper.ToUninstallResult(plan.Record, execution);
    }
}
