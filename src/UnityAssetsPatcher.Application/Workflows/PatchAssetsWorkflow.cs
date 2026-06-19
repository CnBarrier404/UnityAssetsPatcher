using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class PatchAssetsWorkflow
{
    private readonly PatchPlanBuilder _patchPlanBuilder;
    private readonly PatchOutputWriter _patchOutputWriter;

    public PatchAssetsWorkflow(
        PatchPlanBuilder patchPlanBuilder,
        PatchOutputWriter patchOutputWriter)
    {
        _patchPlanBuilder = patchPlanBuilder;
        _patchOutputWriter = patchOutputWriter;
    }

    public PatchAssetPreview Preview(PackageSource source, TargetAssetSet targets, WorkflowTiming timings)
    {
        return new PatchPlanner(_patchPlanBuilder).Preview(source, targets, timings);
    }

    public PatchAssetPlan Plan(PackageSource source, TargetAssetSet targets, WorkflowTiming timings)
    {
        return new PatchPlanner(_patchPlanBuilder).Plan(source, targets, timings);
    }

    public PatchAssetApplyResult Apply(PatchAssetPlan plan, string backupDirectory, WorkflowTiming timings)
    {
        return new PatchAssetApplier(_patchOutputWriter).Execute(plan, backupDirectory, timings);
    }
}
