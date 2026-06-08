using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Application.Installing;

public sealed class InstallPatchPlanBuilder
{
    private readonly PatchAssetsWorkflow _patchAssetsWorkflow;

    public InstallPatchPlanBuilder(PatchAssetsWorkflow patchAssetsWorkflow)
    {
        _patchAssetsWorkflow = patchAssetsWorkflow;
    }

    public IReadOnlyList<InstallPreviewFileResult> CreatePreviews(
        PreparedInstallSource source,
        IReadOnlyList<InstallPatchTarget> targets,
        InstallTimingBuilder timings)
    {
        return timings.MeasureAnalyzeChanges(() => targets
            .Select(target =>
            {
                PatchPreviewResult preview = _patchAssetsWorkflow.PreviewTargets(
                    target.AssetsFilePath,
                    target.Patches,
                    source.ConfigPath);

                return new InstallPreviewFileResult(target.Target, target.AssetsFilePath, preview);
            })
            .ToArray());
    }

    public IReadOnlyList<InstallFilePlan> CreateWritePlans(
        PreparedInstallSource source,
        IReadOnlyList<InstallPatchTarget> targets,
        InstallTimingBuilder timings)
    {
        return timings.MeasureAnalyzeChanges(() => targets
            .Select(target =>
            {
                PatchFileWritePlan patchPlan = _patchAssetsWorkflow.CreateWritePlan(
                    target.AssetsFilePath,
                    target.Patches,
                    source.ConfigPath);

                return new InstallFilePlan(target.Target, target.AssetsFilePath, patchPlan);
            })
            .ToArray());
    }
}
