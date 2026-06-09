using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PatchPlanner
{
    private readonly PatchPlanBuilder _patchPlanBuilder;

    public PatchPlanner(PatchPlanBuilder patchPlanBuilder)
    {
        _patchPlanBuilder = patchPlanBuilder;
    }

    public PatchAssetPreview Preview(
        PackageSource source,
        TargetAssetSet targets,
        WorkflowTiming timings)
    {
        var files = timings.MeasureAnalyzeChanges(() => targets.Targets
            .Select(target =>
            {
                PatchPreviewResult preview = _patchPlanBuilder.CreatePreview(
                    target.AssetsFilePath,
                    target.Patches,
                    source.ConfigPath);

                return new PatchAssetPreviewFile(target.Name, target.AssetsFilePath, preview);
            })
            .ToArray());

        return new PatchAssetPreview(files);
    }

    public PatchAssetPlan Plan(
        PackageSource source,
        TargetAssetSet targets,
        WorkflowTiming timings)
    {
        var files = timings.MeasureAnalyzeChanges(() => targets.Targets
            .Select(target =>
            {
                PatchFileWritePlan patchPlan = _patchPlanBuilder.CreateRequiredWritePlan(
                    target.AssetsFilePath,
                    target.Patches,
                    source.ConfigPath);

                return new PatchAssetFilePlan(target.Name, target.AssetsFilePath, patchPlan);
            })
            .ToArray());

        return new PatchAssetPlan(files);
    }
}

public sealed record PatchAssetPreview(IReadOnlyList<PatchAssetPreviewFile> Files);

public sealed record PatchAssetPreviewFile(string Target, string AssetsFilePath, PatchPreviewResult Preview);

public sealed record PatchAssetPlan(IReadOnlyList<PatchAssetFilePlan> Files);

public sealed record PatchAssetFilePlan(string Target, string AssetsFilePath, PatchFileWritePlan PatchPlan);
