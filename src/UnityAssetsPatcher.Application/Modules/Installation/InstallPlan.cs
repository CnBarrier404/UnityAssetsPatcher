namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed record InstallPlan(
    string GameDirectory,
    TargetAssetSet Targets,
    IReadOnlyList<InstallPayloadFilePlan> PayloadFiles,
    InstallPatchPreview? PatchPreview,
    InstallPatchPlan? PatchWritePlan);
