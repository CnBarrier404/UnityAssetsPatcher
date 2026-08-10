using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Uninstallation;

namespace UnityAssetsPatcher.Application.Repository;

public sealed class Repository : IRepository
{
    private readonly RepositoryService _repositoryService;
    private readonly InstallExecutor _installExecutor;
    private readonly UninstallExecutor _uninstallExecutor;

    public Repository(
        RepositoryService repositoryService,
        InstallExecutor installExecutor,
        UninstallExecutor uninstallExecutor)
    {
        ArgumentNullException.ThrowIfNull(repositoryService);
        ArgumentNullException.ThrowIfNull(installExecutor);
        ArgumentNullException.ThrowIfNull(uninstallExecutor);

        _repositoryService = repositoryService;
        _installExecutor = installExecutor;
        _uninstallExecutor = uninstallExecutor;
    }

    public void Initialize()
    {
        using RepositoryOperationLock operationLock = _repositoryService.AcquireLock();
        _ = CheckPendingTransactions("initializing the repository");
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        return _repositoryService.ListInstalledMods();
    }

    public async Task<RepositoryInstallResult> InstallModAsync(
        InstallModPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        using RepositoryOperationLock operationLock = _repositoryService.AcquireLock();
        RepositoryRecoveryReport recovery = CheckPendingTransactions("installing another mod");
        _ = _repositoryService.RequireWritableMetadata();

        var timings = new StepTimer();
        InstallExecutionResult execution = await _installExecutor.ExecuteAsync(
            plan.PackagePath,
            plan.Analysis,
            operationLock,
            timings,
            plan.ExpectedAssetFiles,
            cancellationToken).ConfigureAwait(false);

        return new RepositoryInstallResult(execution, recovery, timings.BuildSnapshot());
    }

    public async Task<UninstallModResult> UninstallModAsync(
        UninstallPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        using RepositoryOperationLock operationLock = _repositoryService.AcquireLock();
        RepositoryRecoveryReport recovery = CheckPendingTransactions("uninstalling another mod");
        _ = _repositoryService.RequireWritableMetadata();

        UninstallModResult result = await _uninstallExecutor.ExecuteAsync(plan, cancellationToken)
            .ConfigureAwait(false);

        return result with
        {
            Recovery = recovery,
        };
    }

    private RepositoryRecoveryReport CheckPendingTransactions(string operationDescription)
    {
        RepositoryRecoveryReport recovery = _repositoryService.CheckPendingTransactionsUnderLock();

        if (recovery.Status != RepositoryRecoveryStatus.Clean)
        {
            throw new RepositoryRecoveryException(
                recovery.Issues.FirstOrDefault()?.Parameters.GetValueOrDefault("detail") ??
                $"A pending transaction must be recovered before {operationDescription}.",
                recovery);
        }

        return recovery;
    }
}
