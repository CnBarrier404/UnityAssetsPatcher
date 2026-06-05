using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class FindAssetsWorkflow
{
    private readonly AssetQueryService _assetQueryService;
    private readonly IModManifestLoader _manifestLoader;

    public FindAssetsWorkflow(AssetQueryService assetQueryService, IModManifestLoader manifestLoader)
    {
        _assetQueryService = assetQueryService;
        _manifestLoader = manifestLoader;
    }

    public IReadOnlyList<AssetMatch> Find(FindAssetsRequest request)
    {
        ModManifest manifest = _manifestLoader.Load(request.ConfigPath);
        var targets = PatchTargetSelector.ForAssetsFile(manifest, request.AssetsFilePath);

        return _assetQueryService.FindAssetMatches(request.AssetsFilePath, targets);
    }
}
