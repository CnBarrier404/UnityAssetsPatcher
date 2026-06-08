using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly PatchAssetsWorkflow _patchAssetsWorkflow;
    private readonly IModManifestLoader _manifestLoader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;

    public InstallModWorkflow(
        PatchAssetsWorkflow patchAssetsWorkflow,
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver)
    {
        _patchAssetsWorkflow = patchAssetsWorkflow;
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new InstallTimingBuilder();
        using PreparedInstallSource source = new InstallSourcePreparer(_manifestLoader, _gameDirectoryResolver)
            .Prepare(request.ZipFilePath, request.GameDirectory, timings);

        try
        {
            var targets = new InstallTargetPlanBuilder()
                .CreateTargets(source.GameDirectory, source.Manifest, timings);
            var payloadPlanner = new InstallPayloadPlanner();
            var copyPlans = payloadPlanner.CreatePlans(source.ZipFilePath, source.Manifest.Files,
                targets.Select(target => target.AssetsFilePath),
                requireAvailableDestination: false);
            var fileResults = new InstallPatchPlanBuilder(_patchAssetsWorkflow)
                .CreatePreviews(source, targets, timings);

            return new InstallPreviewResult(
                source.Manifest.Name,
                source.Manifest.Version,
                fileResults,
                payloadPlanner.CreatePreviewResults(copyPlans),
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
        using PreparedInstallSource source = new InstallSourcePreparer(_manifestLoader, _gameDirectoryResolver)
            .Prepare(request.ZipFilePath, request.GameDirectory, timings);

        try
        {
            if (!PatchOperationRules.HasPatchOperations(source.Manifest.Patches))
            {
                throw new InvalidOperationException(
                    "Patch config must contain a non-empty 'set', 'add', or 'replaceFrom' operation.");
            }

            var targets = new InstallTargetPlanBuilder()
                .CreateTargets(source.GameDirectory, source.Manifest, timings);
            var payloadPlanner = new InstallPayloadPlanner();
            var copyPlans = payloadPlanner.CreatePlans(source.ZipFilePath, source.Manifest.Files,
                targets.Select(target => target.AssetsFilePath),
                requireAvailableDestination: true);
            var plans = new InstallPatchPlanBuilder(_patchAssetsWorkflow)
                .CreateWritePlans(source, targets, timings);
            var fileResults = new InstallPlanApplier(_patchAssetsWorkflow)
                .ApplyPatches(plans, request.BackupDirectory, timings);
            string zipFilePath = source.ZipFilePath;
            var copiedFiles = timings.MeasureCopyFiles(() => payloadPlanner.CopyFiles(zipFilePath, copyPlans));

            return new InstallModResult(source.Manifest.Name, source.Manifest.Version, fileResults, copiedFiles,
                timings.BuildInstall());
        }
        finally
        {
            _patchAssetsWorkflow.ReleaseReadResources();
        }
    }
}
