using UnityAssetsPatcher.Application.Modules;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class PatchAssetsWorkflow
{
    private readonly PatchPlanner _patchPlanner;
    private readonly PatchAssetApplier _patchAssetApplier;

    public PatchAssetsWorkflow(
        PatchPlanner patchPlanner,
        PatchAssetApplier patchAssetApplier)
    {
        _patchPlanner = patchPlanner;
        _patchAssetApplier = patchAssetApplier;
    }

    public PatchAssetPreview Preview(ModPackage package, TargetAssetSet targets, WorkflowTiming timings)
    {
        return _patchPlanner.Preview(package, targets, timings);
    }

    public PatchAssetPlan Plan(ModPackage package, TargetAssetSet targets, WorkflowTiming timings)
    {
        return _patchPlanner.Plan(package, targets, timings);
    }

    public PatchAssetApplyResult Apply(PatchAssetPlan plan, string backupDirectory, WorkflowTiming timings)
    {
        return _patchAssetApplier.Execute(plan, backupDirectory, timings);
    }
}
