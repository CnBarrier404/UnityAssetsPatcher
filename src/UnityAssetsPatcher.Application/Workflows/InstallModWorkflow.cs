using System.Diagnostics;
using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly PatchAssetsWorkflow _patchAssetsWorkflow;
    private readonly InstallPayloadPlanner _payloadPlanner;

    public InstallModWorkflow(PatchAssetsWorkflow patchAssetsWorkflow, InstallPayloadPlanner payloadPlanner)
    {
        _patchAssetsWorkflow = patchAssetsWorkflow;
        _payloadPlanner = payloadPlanner;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new InstallTimingBuilder();
        EnsureInstallInputsExist(request.ZipFilePath, request.GameDirectory);

        ModManifest manifest = timings.MeasureReadPackage(() => ModManifestLoader.Load(request.ZipFilePath));

        using InstallPackageWorkspace workspace =
            timings.MeasurePrepareSources(() => InstallPackageWorkspace.Prepare(request.ZipFilePath, manifest));
        try
        {
            var targetPaths = timings.MeasureFindGameFiles(() => InstallTargetResolver.Resolve(
                request.GameDirectory,
                manifest.Patches.Select(patch => patch.AssetsFileName)));
            var copyPlans = _payloadPlanner.CreatePlans(request.ZipFilePath, manifest.Files, targetPaths.Values,
                requireAvailableDestination: false);
            var fileResults = timings.MeasureAnalyzeChanges(() =>
                (from targetGroup in
                        manifest.Patches.GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase)
                    let assetsFilePath = targetPaths[targetGroup.Key]
                    let targets = targetGroup.ToArray()
                    let preview = _patchAssetsWorkflow.PreviewTargets(assetsFilePath, targets, workspace.ConfigPath)
                    select new InstallPreviewFileResult(targetGroup.Key, assetsFilePath, preview)).ToList());

            return new InstallPreviewResult(
                manifest.Name,
                manifest.Version,
                fileResults,
                _payloadPlanner.CreatePreviewResults(copyPlans),
                timings.BuildPreview());
        }
        finally
        {
            _patchAssetsWorkflow.ReleaseReadResources();
        }
    }

    public InstallModResult Install(InstallModRequest request)
    {
        var timings = new InstallTimingBuilder();
        EnsureInstallInputsExist(request.ZipFilePath, request.GameDirectory);

        ModManifest manifest = timings.MeasureReadPackage(() => ModManifestLoader.Load(request.ZipFilePath));

        using InstallPackageWorkspace workspace =
            timings.MeasurePrepareSources(() => InstallPackageWorkspace.Prepare(request.ZipFilePath, manifest));
        try
        {
            if (!PatchOperationRules.HasPatchOperations(manifest.Patches))
            {
                throw new InvalidOperationException(
                    "Patch config must contain a non-empty 'set', 'add', or 'replaceFrom' operation.");
            }

            var targetPaths = timings.MeasureFindGameFiles(() => InstallTargetResolver.Resolve(
                request.GameDirectory,
                manifest.Patches.Select(patch => patch.AssetsFileName)));
            var copyPlans = _payloadPlanner.CreatePlans(request.ZipFilePath, manifest.Files, targetPaths.Values,
                requireAvailableDestination: true);
            var plans = timings.MeasureAnalyzeChanges(() =>
                (from targetGroup in
                        manifest.Patches.GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase)
                    let assetsFilePath = targetPaths[targetGroup.Key]
                    let targets = targetGroup.ToArray()
                    let patchPlan = _patchAssetsWorkflow.CreateWritePlan(assetsFilePath, targets, workspace.ConfigPath)
                    select new InstallFilePlan(targetGroup.Key, assetsFilePath, patchPlan)).ToList());

            var fileResults = timings.MeasureApplyPatches(() =>
                (from plan in plans
                    let result =
                        _patchAssetsWorkflow.WritePlanInPlace(plan.AssetsFilePath, request.BackupDirectory,
                            plan.PatchPlan)
                    where result.OperationCount != 0
                    let backupPath =
                        result.BackupPath ??
                        throw new InvalidOperationException("Install patch did not create a backup.")
                    select new InstallModFileResult(plan.Target, result.OutputPath, backupPath, result.AssetCount,
                        result.OperationCount)).ToList());

            var copiedFiles = timings.MeasureCopyFiles(() => _payloadPlanner.CopyFiles(request.ZipFilePath, copyPlans));

            return new InstallModResult(manifest.Name, manifest.Version, fileResults, copiedFiles,
                timings.BuildInstall());
        }
        finally
        {
            _patchAssetsWorkflow.ReleaseReadResources();
        }
    }

    private static void EnsureInstallInputsExist(string zipFilePath, string gameDirectory)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException($"Mod zip file not found: {zipFilePath}", zipFilePath);
        }

        if (!Directory.Exists(gameDirectory))
        {
            throw new DirectoryNotFoundException($"Game directory not found: {gameDirectory}");
        }
    }

    private sealed record InstallFilePlan(
        string Target,
        string AssetsFilePath,
        PatchFileWritePlan PatchPlan);

    private sealed class InstallTimingBuilder
    {
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();

        private TimeSpan _readPackage;
        private TimeSpan _prepareSources;
        private TimeSpan _findGameFiles;
        private TimeSpan _analyzeChanges;
        private TimeSpan? _applyPatches;
        private TimeSpan? _copyFiles;

        public T MeasureReadPackage<T>(Func<T> action)
        {
            return Measure(action, elapsed => _readPackage = elapsed);
        }

        public T MeasurePrepareSources<T>(Func<T> action)
        {
            return Measure(action, elapsed => _prepareSources = elapsed);
        }

        public T MeasureFindGameFiles<T>(Func<T> action)
        {
            return Measure(action, elapsed => _findGameFiles = elapsed);
        }

        public T MeasureAnalyzeChanges<T>(Func<T> action)
        {
            return Measure(action, elapsed => _analyzeChanges = elapsed);
        }

        public T MeasureApplyPatches<T>(Func<T> action)
        {
            return Measure(action, elapsed => _applyPatches = elapsed);
        }

        public T MeasureCopyFiles<T>(Func<T> action)
        {
            return Measure(action, elapsed => _copyFiles = elapsed);
        }

        public InstallTimingResult BuildPreview()
        {
            return Build();
        }

        public InstallTimingResult BuildInstall()
        {
            return Build();
        }

        private InstallTimingResult Build()
        {
            return new InstallTimingResult(
                _readPackage,
                _prepareSources,
                _findGameFiles,
                _analyzeChanges,
                _applyPatches,
                _copyFiles,
                _elapsed.Elapsed);
        }

        private static T Measure<T>(Func<T> action, Action<TimeSpan> setElapsed)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                return action();
            }
            finally
            {
                stopwatch.Stop();
                setElapsed(stopwatch.Elapsed);
            }
        }
    }
}
