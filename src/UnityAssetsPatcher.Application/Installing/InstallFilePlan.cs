using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Installing;

public sealed record InstallFilePlan(string Target, string AssetsFilePath, PatchFileWritePlan PatchPlan);
