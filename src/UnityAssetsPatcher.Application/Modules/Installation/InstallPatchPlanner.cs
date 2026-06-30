using UnityAssetsPatcher.Application.Modules.Patching;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPatchPlanner
{
    private readonly PatchPlanBuilder _patchPlanBuilder;

    public InstallPatchPlanner(PatchPlanBuilder patchPlanBuilder)
    {
        _patchPlanBuilder = patchPlanBuilder;
    }

    public InstallPatchPreview CreatePreview(TargetAssetSet targets, ModPackage package, StepTimer timings)
    {
        var files = timings.Measure("analyze-changes", () => targets.Targets
            .Select(target =>
            {
                var preview = _patchPlanBuilder.CreatePreview(
                    target.AssetsFilePath,
                    target.Patches,
                    package.PatchSourcePaths);

                return new InstallPatchPreviewFile(target.Name, target.AssetsFilePath, preview);
            })
            .ToArray());

        return new InstallPatchPreview(files);
    }

    public InstallPatchPlan CreateRequiredWritePlan(TargetAssetSet targets, ModPackage package, StepTimer timings)
    {
        PatchOperationRules.ValidateModManifest(package.Manifest);

        var files = timings.Measure("analyze-changes", () => targets.Targets
            .Select(target =>
            {
                PatchFileWritePlan patchPlan = _patchPlanBuilder.CreateRequiredWritePlan(
                    target.AssetsFilePath,
                    target.Patches,
                    package.PatchSourcePaths);

                return new InstallPatchPlanFile(target.Name, target.AssetsFilePath, patchPlan);
            })
            .ToArray());

        return new InstallPatchPlan(files);
    }
}
