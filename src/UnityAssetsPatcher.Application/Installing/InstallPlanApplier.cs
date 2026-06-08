using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Application.Installing;

public sealed class InstallPlanApplier
{
    private readonly PatchAssetsWorkflow _patchAssetsWorkflow;

    public InstallPlanApplier(PatchAssetsWorkflow patchAssetsWorkflow)
    {
        _patchAssetsWorkflow = patchAssetsWorkflow;
    }

    public IReadOnlyList<InstallModFileResult> ApplyPatches(
        IReadOnlyList<InstallFilePlan> plans,
        string backupDirectory,
        InstallTimingBuilder timings)
    {
        return timings.MeasureApplyPatches(() => plans
            .Select(plan =>
            {
                PatchApplyResult result = _patchAssetsWorkflow.WritePlanInPlace(
                    plan.AssetsFilePath,
                    backupDirectory,
                    plan.PatchPlan);

                return new { Plan = plan, Result = result };
            })
            .Where(item => item.Result.OperationCount != 0)
            .Select(item =>
            {
                string backupPath = item.Result.BackupPath ??
                                    throw new InvalidOperationException("Install patch did not create a backup.");

                return new InstallModFileResult(
                    item.Plan.Target,
                    item.Result.OutputPath,
                    backupPath,
                    item.Result.AssetCount,
                    item.Result.OperationCount);
            })
            .ToArray());
    }
}
