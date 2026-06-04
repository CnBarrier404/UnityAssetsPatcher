using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class FindAssetsWorkflow
{
    private readonly AssetQueryService _assetQueryService;

    public FindAssetsWorkflow(AssetQueryService assetQueryService)
    {
        _assetQueryService = assetQueryService;
    }

    public IReadOnlyList<AssetMatch> Find(FindAssetsRequest request)
    {
        ModManifest manifest = ModManifestLoader.Load(request.ConfigPath);
        var targets = PatchTargetSelector.ForAssetsFile(manifest, request.AssetsFilePath);

        return _assetQueryService.FindAssetMatches(request.AssetsFilePath, targets);
    }
}
