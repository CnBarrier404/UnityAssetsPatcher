using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Backups;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly InstallPlanner _planner;
    private readonly InstallExecutor _executor;
    private readonly ModBackupStore _backupStore;

    public InstallModWorkflow(
        InstallPlanner planner,
        InstallExecutor executor,
        ModBackupStore backupStore)
    {
        _planner = planner;
        _executor = executor;
        _backupStore = backupStore;
    }

    public InstallPreviewResult Preview(InstallRequest request)
    {
        var timings = new StepTimer();

        try
        {
            using InstallPlanSession<InstallPreviewPlan> session = _planner.BuildPreview(request, timings);
            InstallPreviewPlan preview = session.Plan;

            return InstallResultMapper.ToPreviewResult(
                session.Package,
                preview.PatchFiles,
                preview.Payload,
                timings.BuildSnapshot());
        }
        finally
        {
            _executor.CloseReadSessions();
        }
    }

    public InstallModResult Install(InstallRequest request)
    {
        var timings = new StepTimer();

        try
        {
            using BackupOperationLock operationLock = _backupStore.AcquireOperationLock();
            using InstallPlanSession<InstallWritePlan> session = _planner.BuildInstall(request, timings);

            InstallExecutionResult execution = _executor.Execute(session, timings);

            return InstallResultMapper.ToInstallResult(
                session.Package,
                execution.PatchedFiles,
                execution.CopiedFiles,
                timings.BuildSnapshot());
        }
        finally
        {
            _executor.CloseReadSessions();
        }
    }
}
