using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class FindAssetsWorkflow
{
    private readonly AssetQueryService _assetQueryService;
    private readonly IModManifestLoader _manifestLoader;
    private readonly ManifestTargetSelector _targetSelector;

    public FindAssetsWorkflow(
        AssetQueryService assetQueryService,
        IModManifestLoader manifestLoader,
        ManifestTargetSelector targetSelector)
    {
        _assetQueryService = assetQueryService;
        _manifestLoader = manifestLoader;
        _targetSelector = targetSelector;
    }

    public IReadOnlyList<AssetMatch> Find(FindAssetsRequest request)
    {
        ModManifest manifest = _manifestLoader.Load(request.ConfigPath);
        var targets = _targetSelector.ForAssetsFile(manifest, request.AssetsFilePath);

        return _assetQueryService
            .FindMatches(request.AssetsFilePath, targets)
            .Select(match => new AssetMatch(match.Asset, match.IncludeGroup))
            .ToArray();
    }
}
