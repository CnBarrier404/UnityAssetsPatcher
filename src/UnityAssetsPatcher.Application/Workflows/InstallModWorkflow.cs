using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly ModPackageArchiveService _archiveService;
    private readonly InstallPlanBuilder _planBuilder;
    private readonly InstallExecutor _executor;
    private readonly BackupRepository _backupRepository;
    private readonly IAssetsAccessScopeFactory _assetsAccessScopeFactory;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<InstallModWorkflow> _logger;

    public InstallModWorkflow(
        ModPackageArchiveService archiveService,
        InstallPlanBuilder planBuilder,
        InstallExecutor executor,
        BackupRepository backupRepository,
        IAssetsAccessScopeFactory assetsAccessScopeFactory,
        IFileSystemOperations fileSystemOperations,
        ILogger<InstallModWorkflow>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(archiveService);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _archiveService = archiveService;
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
        PreparedInstall preparedInstall = CreatePreparedInstall(request, package, analysis);

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
            _archiveService,
            _fileSystemOperations,
            timings);
        using IAssetsAccessScope assetsScope = _assetsAccessScopeFactory.CreateScope();
        PreparedInstall? preparedInstall = request.PreparedInstall;
        InstallAnalysis analysis = preparedInstall is null
            ? _planBuilder.Analyze(
                package,
                request.GameDirectory,
                InstallAnalysisMode.Apply,
                assetsScope.Reader,
                timings)
            : PrepareAnalysis(request, package, preparedInstall);

        InstallExecutionResult execution = _executor.Execute(
            package,
            analysis,
            assetsScope.Writer,
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
                timings.BuildSnapshot()) with
            {
                Recovery = recovery,
            };
    }

    private PreparedInstall CreatePreparedInstall(
        InstallRequest request,
        ModPackage package,
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
            analysis,
            _fileSystemOperations.ComputeFileIntegrity(zipFilePath),
            [
                .. assetFilePaths.Select(path => new PreparedInstallAssetFile(
                    path,
                    _fileSystemOperations.ComputeFileIntegrity(path)))
            ],
            new Dictionary<string, string>(package.PatchSourcePaths, StringComparer.OrdinalIgnoreCase));
    }

    private InstallAnalysis PrepareAnalysis(
        InstallRequest request,
        ModPackage package,
        PreparedInstall preparedInstall)
    {
        ValidatePreparedInstall(request, preparedInstall);
        PatchOperationRules.ValidateModManifest(package.Manifest);

        foreach (InstallTargetAnalysis target in preparedInstall.Analysis.Targets)
        {
            if (target.PlanningResult.CanApply)
            {
                continue;
            }

            PatchDiagnostic diagnostic = target.PlanningResult.Diagnostic ??
                                         throw new InvalidOperationException(
                                             "Prepared install analysis did not contain an applicable patch plan.");
            throw new PatchPlanningException(diagnostic);
        }

        return RebindReplacementSources(
            preparedInstall.Analysis,
            preparedInstall.ReplacementSourcePaths,
            package.PatchSourcePaths);
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

    private static InstallAnalysis RebindReplacementSources(
        InstallAnalysis analysis,
        IReadOnlyDictionary<string, string> previewSourcePaths,
        IReadOnlyDictionary<string, string> currentSourcePaths)
    {
        InstallTargetAnalysis[] targets =
        [
            .. analysis.Targets.Select(target => target with
            {
                PlanningResult = target.PlanningResult with
                {
                    Plan = RebindPatchPlan(target.PlanningResult.Plan, previewSourcePaths, currentSourcePaths),
                },
            }),
        ];

        return analysis with { Targets = targets };
    }

    private static PatchPlan? RebindPatchPlan(
        PatchPlan? plan,
        IReadOnlyDictionary<string, string> previewSourcePaths,
        IReadOnlyDictionary<string, string> currentSourcePaths)
    {
        return plan switch
        {
            AssetReplacementPlan replacementPlan => new AssetReplacementPlan(
                replacementPlan.Replacements
                    .Select(replacement => replacement with
                    {
                        SourceAssetsFilePath = RebindReplacementSourcePath(
                            replacement,
                            previewSourcePaths,
                            currentSourcePaths),
                    })
                    .ToArray()),
            _ => plan,
        };
    }

    private static string RebindReplacementSourcePath(
        AssetReplacement replacement,
        IReadOnlyDictionary<string, string> previewSourcePaths,
        IReadOnlyDictionary<string, string> currentSourcePaths)
    {
        foreach ((string sourceEntryPath, string previewSourcePath) in previewSourcePaths)
        {
            if (!TrustedPath.PathsEqual(previewSourcePath, replacement.SourceAssetsFilePath))
            {
                continue;
            }

            if (!currentSourcePaths.TryGetValue(sourceEntryPath, out string? currentSourcePath))
            {
                throw new InstallPreparationStaleException(
                    $"The replacement source is missing from the current mod package: {sourceEntryPath}");
            }

            return currentSourcePath;
        }

        throw new InstallPreparationStaleException(
            $"The replacement source from the install preview is no longer available: " +
            replacement.SourceAssetsFilePath);
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
