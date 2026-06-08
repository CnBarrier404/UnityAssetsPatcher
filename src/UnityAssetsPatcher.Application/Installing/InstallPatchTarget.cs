using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Installing;

public sealed record InstallPatchTarget(string Target, string AssetsFilePath, IReadOnlyList<ManifestPatch> Patches);
