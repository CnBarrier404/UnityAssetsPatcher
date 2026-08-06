using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
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

    public RepositoryInstallResult InstallMod(InstallModPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        using RepositoryOperationLock operationLock = _repositoryService.AcquireLock();
        RepositoryRecoveryReport recovery = CheckPendingTransactions("installing another mod");
        _ = _repositoryService.RequireWritableMetadata();

        var timings = new StepTimer();
        InstallExecutionResult execution = _installExecutor.Execute(
            plan.PackagePath,
            plan.Analysis,
            operationLock,
            timings,
            plan.ExpectedAssetFiles);

        return new RepositoryInstallResult(execution, recovery, timings.BuildSnapshot());
    }

    public UninstallModResult UninstallMod(UninstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        using RepositoryOperationLock operationLock = _repositoryService.AcquireLock();
        RepositoryRecoveryReport recovery = CheckPendingTransactions("uninstalling another mod");
        _ = _repositoryService.RequireWritableMetadata();

        return _uninstallExecutor.Execute(plan) with
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
