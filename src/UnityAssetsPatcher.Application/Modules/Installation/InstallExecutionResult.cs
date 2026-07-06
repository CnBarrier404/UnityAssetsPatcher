using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed record InstallExecutionResult(
    InstallPatchApplyResult PatchApplyResult,
    IReadOnlyList<InstallChange> CopiedFiles);
