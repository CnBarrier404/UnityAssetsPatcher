using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly ModManifestReader _manifestReader;
    private readonly InstallPlanBuilder _planBuilder;
    private readonly InstallExecutor _executor;
    private readonly BackupRepository _backupRepository;
    private readonly IAssetsAccessScopeFactory _assetsAccessScopeFactory;

    public InstallModWorkflow(
        ModManifestReader manifestReader,
        InstallPlanBuilder planBuilder,
        InstallExecutor executor,
        BackupRepository backupRepository,
        IAssetsAccessScopeFactory assetsAccessScopeFactory)
    {
        _manifestReader = manifestReader;
        _planBuilder = planBuilder;
        _executor = executor;
        _backupRepository = backupRepository;
        _assetsAccessScopeFactory = assetsAccessScopeFactory;
    }

    public InstallPreviewResult Preview(InstallRequest request)
    {
        var timings = new StepTimer();
        using ModPackage package = ModPackage.Open(
            request.ZipFilePath,
            request.SelectedOptionalGroups,
            _manifestReader,
            timings);
        using IAssetsAccessScope assetsScope = _assetsAccessScopeFactory.CreateScope();
        InstallAnalysisMode mode = request.IncludePatchPreviewDetails
            ? InstallAnalysisMode.PreviewDetailed
            : InstallAnalysisMode.PreviewSummary;
        InstallAnalysis analysis = _planBuilder.Analyze(
            package,
            request.GameDirectory,
            mode,
            assetsScope.Reader,
            timings);

        return InstallResultMapper.ToPreviewResult(
            analysis,
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

        using ModPackage package = ModPackage.Open(
            request.ZipFilePath,
            request.SelectedOptionalGroups,
            _manifestReader,
            timings);
        using IAssetsAccessScope assetsScope = _assetsAccessScopeFactory.CreateScope();
        InstallAnalysis analysis = _planBuilder.Analyze(
            package,
            request.GameDirectory,
            InstallAnalysisMode.Apply,
            assetsScope.Reader,
            timings);

        assetsScope.CloseReadSessions();
        InstallExecutionResult execution = _executor.Execute(
            package,
            analysis,
            assetsScope.Writer,
            timings);

        return InstallResultMapper.ToInstallResult(
                analysis,
                execution.PatchedFiles,
                execution.CopiedFiles,
                execution.InstallId,
                timings.BuildSnapshot()) with
            {
                Recovery = recovery,
            };
    }
}
