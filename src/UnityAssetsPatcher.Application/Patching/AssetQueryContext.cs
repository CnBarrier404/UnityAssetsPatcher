using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class AssetQueryContext
{
    public IReadOnlyDictionary<long, AssetInfo> AssetsByPathId => _assetsByPathId.Value;

    private IReadOnlyList<AssetInfo> Assets { get; }

    private readonly IAssetsFileReader _assetsReader;
    private readonly string _assetsFilePath;
    private readonly Lazy<IReadOnlyDictionary<long, AssetInfo>> _assetsByPathId;

    private readonly Dictionary<string, IReadOnlyList<AssetInfo>>
        _assetsByType = new(StringComparer.OrdinalIgnoreCase);

    public AssetQueryContext(IAssetsFileReader assetsReader, string assetsFilePath)
    {
        _assetsReader = assetsReader;
        _assetsFilePath = assetsFilePath;
        Assets = assetsReader.ReadAssets(assetsFilePath).ToArray();
        _assetsByPathId =
            new Lazy<IReadOnlyDictionary<long, AssetInfo>>(() => Assets.ToDictionary(asset => asset.PathId));
    }

    public IReadOnlyList<AssetInfo> GetAssetsByType(string assetTypeName)
    {
        if (_assetsByType.TryGetValue(assetTypeName, out var assets))
        {
            return assets;
        }

        assets = Assets
            .Where(asset => string.Equals(asset.TypeName, assetTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _assetsByType.Add(assetTypeName, assets);

        return assets;
    }

    public AssetField ReadField(long pathId)
    {
        return _assetsReader.ReadField(_assetsFilePath, pathId);
    }
}
