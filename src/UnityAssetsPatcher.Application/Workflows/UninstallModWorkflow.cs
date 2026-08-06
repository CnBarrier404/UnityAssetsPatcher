using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Uninstallation;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class UninstallModWorkflow
{
    private readonly UninstallPlanner _planner;
    private readonly UninstallExecutor _executor;
    private readonly RepositoryService _repositoryService;
    private readonly ILogger<UninstallModWorkflow> _logger;

    public UninstallModWorkflow(
        UninstallPlanner planner,
        UninstallExecutor executor,
        RepositoryService repositoryService,
        ILogger<UninstallModWorkflow>? logger = null)
    {
        _planner = planner;
        _executor = executor;
        _repositoryService = repositoryService;
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
        using RepositoryOperationLock operationLock = _repositoryService.AcquireLock();
        RepositoryRecoveryReport recovery = _repositoryService.CheckPendingTransactionsUnderLock();
        if (recovery.Status != RepositoryRecoveryStatus.Clean)
        {
            throw new RepositoryRecoveryException(
                recovery.Issues.FirstOrDefault()?.Parameters.GetValueOrDefault("detail") ??
                "A pending transaction must be recovered before uninstalling another mod.",
                recovery);
        }

        _ = _repositoryService.RequireWritableMetadata();
        UninstallPlan plan = _planner.BuildUninstall(request);
        UninstallModResult result = _executor.Execute(plan) with { Recovery = recovery };

        _logger.LogInformation(
            "Uninstalled {ModName} {ModVersion}: {ChangedFileCount} files composed",
            result.ModName,
            result.ModVersion,
            result.ChangedFiles.Count);

        return result;
    }
}
