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
        return _planner.BuildPreview(request);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        using BackupOperationLock operationLock = _backupStore.AcquireOperationLock();
        UninstallPlan plan = _planner.BuildUninstall(request);
        return _executor.Execute(plan);
    }
}
