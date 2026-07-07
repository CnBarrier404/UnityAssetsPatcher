using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules.Uninstallation;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class UninstallModWorkflow
{
    private readonly UninstallPlanner _planner;
    private readonly UninstallExecutor _executor;

    public UninstallModWorkflow(UninstallPlanner planner, UninstallExecutor executor)
    {
        _planner = planner;
        _executor = executor;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return _planner.ListInstalled();
    }

    public UninstallPreviewResult Preview(UninstallPreviewRequest request)
    {
        UninstallPreviewPlan plan = _planner.BuildPreview(request);

        return UninstallResultMapper.ToPreviewResult(plan);
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        UninstallPlan plan = _planner.BuildUninstall(request);
        UninstallExecutionResult execution = _executor.Execute(plan);

        return UninstallResultMapper.ToUninstallResult(plan.Record, execution);
    }
}
