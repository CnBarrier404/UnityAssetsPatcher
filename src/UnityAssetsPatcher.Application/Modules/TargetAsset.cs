using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules;

public sealed record TargetAsset(string Name, string AssetsFilePath, IReadOnlyList<ManifestPatch> Patches);
