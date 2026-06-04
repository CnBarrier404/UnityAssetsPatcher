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
        var targetPaths = InstallTargetResolver.Resolve(
            request.GameDirectory,
            manifest.Patches.Select(patch => patch.AssetsFileName));
        var copyPlans = _payloadPlanner.CreatePlans(request.ZipFilePath, manifest.Files, targetPaths.Values,
            requireAvailableDestination: false);
        var fileResults = new List<InstallPreviewFileResult>();

        foreach (var targetGroup in manifest.Patches
                     .GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase))
        {
            string assetsFilePath = targetPaths[targetGroup.Key];
            var targets = targetGroup.ToArray();
            PatchPreviewResult preview = _patchAssetsWorkflow.PreviewTargets(assetsFilePath, targets,
                workspace.ConfigPath);
            fileResults.Add(new InstallPreviewFileResult(targetGroup.Key, assetsFilePath, preview));
        }

        return new InstallPreviewResult(
            manifest.Name,
            manifest.Version,
            fileResults,
            _payloadPlanner.CreatePreviewResults(copyPlans));
    }

    public InstallModResult Install(InstallModRequest request)
    {
        EnsureInstallInputsExist(request.ZipFilePath, request.GameDirectory);

        ModManifest manifest = ModManifestLoader.Load(request.ZipFilePath);

        using InstallPackageWorkspace workspace = InstallPackageWorkspace.Prepare(request.ZipFilePath, manifest);

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
        var plans = new List<InstallFilePlan>();

        foreach (var targetGroup in manifest.Patches
                     .GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase))
        {
            string assetsFilePath = targetPaths[targetGroup.Key];
            var targets = targetGroup.ToArray();
            PatchFileWritePlan patchPlan = _patchAssetsWorkflow.CreateWritePlan(assetsFilePath, targets,
                workspace.ConfigPath);

            plans.Add(new InstallFilePlan(targetGroup.Key, assetsFilePath, patchPlan));
        }

        var fileResults = new List<InstallModFileResult>();

        foreach (InstallFilePlan plan in plans)
        {
            PatchApplyResult result = _patchAssetsWorkflow.WritePlanInPlace(
                plan.AssetsFilePath,
                request.BackupDirectory,
                plan.PatchPlan);

            if (result.OperationCount == 0)
            {
                continue;
            }

            string backupPath = result.BackupPath ??
                                throw new InvalidOperationException("Install patch did not create a backup.");
            fileResults.Add(new InstallModFileResult(
                plan.Target,
                result.OutputPath,
                backupPath,
                result.AssetCount,
                result.OperationCount));
        }

        IReadOnlyList<InstallCopiedFileResult> copiedFiles =
            _payloadPlanner.CopyFiles(request.ZipFilePath, copyPlans);

        return new InstallModResult(manifest.Name, manifest.Version, fileResults, copiedFiles);
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
