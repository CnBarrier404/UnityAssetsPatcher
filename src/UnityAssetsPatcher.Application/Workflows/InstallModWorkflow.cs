using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly ModPackageArchiveService _archiveService;
    private readonly InstallPlanBuilder _planBuilder;
    private readonly InstallExecutor _executor;
    private readonly RepositoryService _repositoryService;
    private readonly IAssetsAccessScopeFactory _assetsAccessScopeFactory;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<InstallModWorkflow> _logger;

    public InstallModWorkflow(
        ModPackageArchiveService archiveService,
        InstallPlanBuilder planBuilder,
        InstallExecutor executor,
        RepositoryService repositoryService,
        IAssetsAccessScopeFactory assetsAccessScopeFactory,
        IFileSystemOperations fileSystemOperations,
        ILogger<InstallModWorkflow>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(archiveService);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _archiveService = archiveService;
        _planBuilder = planBuilder;
        _executor = executor;
        _repositoryService = repositoryService;
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
            _archiveService,
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
        PreparedInstall preparedInstall = CreatePreparedInstall(request, analysis);

        return InstallResultMapper.ToPreviewResult(
                analysis,
                timings.BuildSnapshot()) with
            {
                PreparedInstall = preparedInstall,
            };
    }

    public InstallModResult Install(InstallRequest request)
    {
        _logger.LogInformation("Installing mod from {ZipFilePath}", request.ZipFilePath);
        var timings = new StepTimer();

        using RepositoryOperationLock operationLock = _repositoryService.AcquireLock();
        RepositoryRecoveryReport recovery = _repositoryService.CheckPendingTransactionsUnderLock();

        if (recovery.Status != RepositoryRecoveryStatus.Clean)
        {
            throw new RepositoryRecoveryException(
                recovery.Issues.FirstOrDefault()?.Parameters.GetValueOrDefault("detail") ??
                "A pending transaction must be recovered before installing another mod.",
                recovery);
        }

        _ = _repositoryService.RequireWritableMetadata();

        using ModPackage package = ModPackage.Open(
            request.ZipFilePath,
            request.SelectedOptionalGroups,
            _archiveService,
            _fileSystemOperations,
            timings);
        PreparedInstall? preparedInstall = request.PreparedInstall;
        InstallAnalysis analysis;

        using (IAssetsAccessScope assetsScope = _assetsAccessScopeFactory.CreateScope())
        {
            analysis = preparedInstall is null
                ? _planBuilder.Analyze(
                    package,
                    request.GameDirectory,
                    InstallAnalysisMode.Apply,
                    assetsScope.Reader,
                    timings)
                : PrepareAnalysis(request, package, preparedInstall, assetsScope.Reader, timings);
        }

        InstallExecutionResult execution = _executor.Execute(
            request.ZipFilePath,
            package,
            analysis,
            operationLock,
            timings,
            preparedInstall?.AssetFiles);

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
                execution.BaseSnapshotCount,
                timings.BuildSnapshot()) with
            {
                Recovery = recovery,
            };
    }

    private PreparedInstall CreatePreparedInstall(
        InstallRequest request,
        InstallAnalysis analysis)
    {
        string zipFilePath = TrustedPath.NormalizeAbsolutePath(request.ZipFilePath);
        string? gameDirectory = NormalizeOptionalPath(request.GameDirectory);
        string[] assetFilePaths = analysis.Targets
            .Select(target => target.AssetsFilePath)
            .Distinct(TrustedPath.PathComparer)
            .ToArray();

        return new PreparedInstall(
            zipFilePath,
            gameDirectory,
            request.SelectedOptionalGroups.ToArray(),
            _fileSystemOperations.ComputeFileIntegrity(zipFilePath),
            [
                .. assetFilePaths.Select(path => new PreparedInstallAssetFile(
                    path,
                    _fileSystemOperations.ComputeFileIntegrity(path)))
            ]);
    }

    private InstallAnalysis PrepareAnalysis(
        InstallRequest request,
        ModPackage package,
        PreparedInstall preparedInstall,
        IAssetsFileReader assetsReader,
        StepTimer timings)
    {
        ValidatePreparedInstall(request, preparedInstall);

        return _planBuilder.Analyze(
            package,
            request.GameDirectory,
            InstallAnalysisMode.Apply,
            assetsReader,
            timings);
    }

    private void ValidatePreparedInstall(InstallRequest request, PreparedInstall preparedInstall)
    {
        if (!TrustedPath.PathsEqual(request.ZipFilePath, preparedInstall.ZipFilePath))
        {
            throw new InstallPreparationStaleException(
                "The install preview does not match the selected mod package.");
        }

        string? gameDirectory = NormalizeOptionalPath(request.GameDirectory);
        if (!PathsEqual(gameDirectory, preparedInstall.GameDirectory))
        {
            throw new InstallPreparationStaleException(
                "The install preview does not match the selected game directory.");
        }

        if (!OptionalGroupsMatch(request.SelectedOptionalGroups, preparedInstall.SelectedOptionalGroups))
        {
            throw new InstallPreparationStaleException(
                "The install preview does not match the selected optional groups.");
        }

        FileIntegrity actualPackageIntegrity = _fileSystemOperations.ComputeFileIntegrity(request.ZipFilePath);
        if (!preparedInstall.PackageIntegrity.Matches(actualPackageIntegrity))
        {
            throw new InstallPreparationStaleException(
                "The mod package changed after the install preview.");
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return TrustedPath.PathsEqual(left, right);
    }

    private static bool OptionalGroupsMatch(
        IReadOnlyList<string> selectedOptionalGroups,
        IReadOnlyList<string> preparedOptionalGroups)
    {
        HashSet<string> selected = selectedOptionalGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> prepared = preparedOptionalGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selected.SetEquals(prepared);
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : TrustedPath.NormalizeAbsolutePath(path);
    }
}
