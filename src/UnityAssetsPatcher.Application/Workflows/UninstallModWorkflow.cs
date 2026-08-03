using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Uninstallation;
using InstallRecordSummary = UnityAssetsPatcher.Application.Contracts.InstallRecordSummary;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class UninstallModWorkflow
{
    private readonly UninstallPlanner _planner;
    private readonly UninstallExecutor _executor;
    private readonly BackupRepository _backupRepository;
    private readonly ILogger<UninstallModWorkflow> _logger;

    public UninstallModWorkflow(
        UninstallPlanner planner,
        UninstallExecutor executor,
        BackupRepository backupRepository,
        ILogger<UninstallModWorkflow>? logger = null)
    {
        _planner = planner;
        _executor = executor;
        _backupRepository = backupRepository;
        _logger = logger ?? NullLogger<UninstallModWorkflow>.Instance;
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
        _logger.LogInformation("Uninstalling mod install {InstallId}", request.InstallId);
        using BackupOperationLock operationLock = _backupRepository.AcquireLock();
        BackupRecoveryReport recovery = _backupRepository.CheckPendingTransactionsUnderLock();
        if (recovery.Status != BackupRepositoryStatus.Clean)
        {
            throw new BackupRecoveryException(
                recovery.Issues.FirstOrDefault()?.Parameters.GetValueOrDefault("detail") ??
                "A pending transaction must be recovered before uninstalling another mod.",
                recovery);
        }

        UninstallPlan plan = _planner.BuildUninstall(request);
        UninstallModResult result = _executor.Execute(plan) with { Recovery = recovery };

        _logger.LogInformation(
            "Uninstalled {ModName} {ModVersion}: {RestoredFileCount} files restored, {DeletedFileCount} files deleted",
            result.ModName,
            result.ModVersion,
            result.RestoredFiles.Count,
            result.DeletedFiles.Count);

        return result;
    }
}
