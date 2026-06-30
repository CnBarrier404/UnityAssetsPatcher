using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules.Patching;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed record InstallPatchPreview(IReadOnlyList<InstallPatchPreviewFile> Files);

public sealed record InstallPatchPreviewFile(string Target, string AssetsFilePath, PatchPreviewResult Preview);

public sealed record InstallPatchPlan(IReadOnlyList<InstallPatchPlanFile> Files);

public sealed record InstallPatchPlanFile(string Target, string AssetsFilePath, PatchFileWritePlan PatchPlan);

public sealed record InstallPatchApplyResult(IReadOnlyList<InstallPatchAppliedFile> Files);

public sealed record InstallPatchAppliedFile(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);
