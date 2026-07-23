using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Abstractions.Assets;

public interface IAssetsFileReader : IDisposable
{
    public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath);
    public AssetField ReadField(string assetsFilePath, long pathId);
}
