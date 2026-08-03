using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly ModManifestReader _manifestReader;
    private readonly InstallPlanBuilder _planBuilder;
    private readonly InstallExecutor _executor;
    private readonly BackupRepository _backupRepository;
    private readonly IAssetsAccessScopeFactory _assetsAccessScopeFactory;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<InstallModWorkflow> _logger;

    public InstallModWorkflow(
        ModManifestReader manifestReader,
        InstallPlanBuilder planBuilder,
        InstallExecutor executor,
        BackupRepository backupRepository,
        IAssetsAccessScopeFactory assetsAccessScopeFactory,
        IFileSystemOperations fileSystemOperations,
        ILogger<InstallModWorkflow>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _manifestReader = manifestReader;
        _planBuilder = planBuilder;
        _executor = executor;
        _backupRepository = backupRepository;
        _assetsAccessScopeFactory = assetsAccessScopeFactory;
        _fileSystemOperations = fileSystemOperations;
        _logger = logger ?? NullLogger<InstallModWorkflow>.Instance;
    }

    public InstallPreviewResult Preview(InstallRequest request)
    {
        _logger.LogInformation("Previewing mod install from {ZipFilePath}", request.ZipFilePath);
        var timings = new StepTimer();
        using ModPackage package = ModPackage.Open(
            request.ZipFilePath,
            request.SelectedOptionalGroups,
            _manifestReader,
            _fileSystemOperations,
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
        _logger.LogInformation("Installing mod from {ZipFilePath}", request.ZipFilePath);
        var timings = new StepTimer();

        using BackupOperationLock operationLock = _backupRepository.AcquireLock();
        BackupRecoveryReport recovery = _backupRepository.CheckPendingTransactionsUnderLock();

        if (recovery.Status != BackupRepositoryStatus.Clean)
        {
            throw new BackupRecoveryException(
                recovery.Issues.FirstOrDefault()?.Parameters.GetValueOrDefault("detail") ??
                "A pending transaction must be recovered before installing another mod.",
                recovery);
        }

        using ModPackage package = ModPackage.Open(
            request.ZipFilePath,
            request.SelectedOptionalGroups,
            _manifestReader,
            _fileSystemOperations,
            timings);
        using IAssetsAccessScope assetsScope = _assetsAccessScopeFactory.CreateScope();
        InstallAnalysis analysis = _planBuilder.Analyze(
            package,
            request.GameDirectory,
            InstallAnalysisMode.Apply,
            assetsScope.Reader,
            timings);

        InstallExecutionResult execution = _executor.Execute(
            package,
            analysis,
            assetsScope.Writer,
            timings);

        _logger.LogInformation(
            "Installed {ModName} {ModVersion}: {PatchedFileCount} files patched, {CopiedFileCount} files copied, install id {InstallId}",
            analysis.Manifest.Name,
            analysis.Manifest.Version,
            execution.PatchedFiles.Count,
            execution.CopiedFiles.Count,
            execution.InstallId);

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
