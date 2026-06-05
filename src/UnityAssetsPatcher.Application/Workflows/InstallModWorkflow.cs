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
        EnsureInstallInputsExist(request.ZipFilePath, request.GameDirectory);

        ModManifest manifest = ModManifestLoader.Load(request.ZipFilePath);

        using InstallPackageWorkspace workspace = InstallPackageWorkspace.Prepare(request.ZipFilePath, manifest);
        try
        {
            var targetPaths = InstallTargetResolver.Resolve(
                request.GameDirectory,
                manifest.Patches.Select(patch => patch.AssetsFileName));
            var copyPlans = _payloadPlanner.CreatePlans(request.ZipFilePath, manifest.Files, targetPaths.Values,
                requireAvailableDestination: false);
            var fileResults =
                (from targetGroup in
                        manifest.Patches.GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase)
                    let assetsFilePath = targetPaths[targetGroup.Key]
                    let targets = targetGroup.ToArray()
                    let preview = _patchAssetsWorkflow.PreviewTargets(assetsFilePath, targets, workspace.ConfigPath)
                    select new InstallPreviewFileResult(targetGroup.Key, assetsFilePath, preview)).ToList();

            return new InstallPreviewResult(
                manifest.Name,
                manifest.Version,
                fileResults,
                _payloadPlanner.CreatePreviewResults(copyPlans));
        }
        finally
        {
            _patchAssetsWorkflow.ReleaseReadResources();
        }
    }

    public InstallModResult Install(InstallModRequest request)
    {
        EnsureInstallInputsExist(request.ZipFilePath, request.GameDirectory);

        ModManifest manifest = ModManifestLoader.Load(request.ZipFilePath);

        using InstallPackageWorkspace workspace = InstallPackageWorkspace.Prepare(request.ZipFilePath, manifest);
        try
        {
            if (!PatchOperationRules.HasPatchOperations(manifest.Patches))
            {
                throw new InvalidOperationException(
                    "Patch config must contain a non-empty 'set', 'add', or 'replaceFrom' operation.");
            }

            var targetPaths = InstallTargetResolver.Resolve(
                request.GameDirectory,
                manifest.Patches.Select(patch => patch.AssetsFileName));
            var copyPlans = _payloadPlanner.CreatePlans(request.ZipFilePath, manifest.Files, targetPaths.Values,
                requireAvailableDestination: true);
            var plans =
                (from targetGroup in
                        manifest.Patches.GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase)
                    let assetsFilePath = targetPaths[targetGroup.Key]
                    let targets = targetGroup.ToArray()
                    let patchPlan = _patchAssetsWorkflow.CreateWritePlan(assetsFilePath, targets, workspace.ConfigPath)
                    select new InstallFilePlan(targetGroup.Key, assetsFilePath, patchPlan)).ToList();

            var fileResults = (from plan in plans
                let result =
                    _patchAssetsWorkflow.WritePlanInPlace(plan.AssetsFilePath, request.BackupDirectory, plan.PatchPlan)
                where result.OperationCount != 0
                let backupPath =
                    result.BackupPath ?? throw new InvalidOperationException("Install patch did not create a backup.")
                select new InstallModFileResult(plan.Target, result.OutputPath, backupPath, result.AssetCount,
                    result.OperationCount)).ToList();

            var copiedFiles =
                _payloadPlanner.CopyFiles(request.ZipFilePath, copyPlans);

            return new InstallModResult(manifest.Name, manifest.Version, fileResults, copiedFiles);
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
}
