using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Backups;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly InstallPlanner _planner;
    private readonly InstallExecutor _executor;
    private readonly BackupRepository _backupRepository;

    public InstallModWorkflow(
        InstallPlanner planner,
        InstallExecutor executor,
        BackupRepository backupRepository)
    {
        _planner = planner;
        _executor = executor;
        _backupRepository = backupRepository;
    }

    public InstallPreviewResult Preview(InstallRequest request)
    {
        var timings = new StepTimer();
        using var session = _planner.BuildPreview(request, timings);
        InstallPreviewPlan preview = session.Plan;

        return InstallResultMapper.ToPreviewResult(
            session.Package,
            preview.PatchFiles,
            preview.Payload,
            timings.BuildSnapshot());
    }

    public InstallModResult Install(InstallRequest request)
    {
        var timings = new StepTimer();

        using BackupOperationLock operationLock = _backupRepository.AcquireLock();
        BackupRecoveryReport recovery = _backupRepository.CheckPendingTransactionsUnderLock();

        if (recovery.Status != BackupRepositoryStatus.Clean)
        {
            throw new BackupRecoveryException(
                recovery.Issues.FirstOrDefault()?.Message ??
                "A pending transaction must be recovered before installing another mod.",
                recovery);
        }

        using var session = _planner.BuildInstall(request, timings);

        InstallExecutionResult execution = _executor.Execute(session, timings);

        return InstallResultMapper.ToInstallResult(
                session.Package,
                execution.PatchedFiles,
                execution.CopiedFiles,
                execution.InstallId,
                timings.BuildSnapshot()) with
            {
                Recovery = recovery,
            };
    }
}
