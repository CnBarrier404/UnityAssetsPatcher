using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Patching;

public static class PatchErrorCodes
{
    public static OperationErrorCode PlanningFailed { get; } = new("patch.planning_failed");
}
