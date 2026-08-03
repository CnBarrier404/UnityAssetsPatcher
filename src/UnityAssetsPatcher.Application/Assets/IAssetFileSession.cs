using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Assets;

public interface IAssetFileSession : IDisposable
{
    public IReadOnlyList<AssetInfo> ReadAssets();

    public AssetField ReadField(AssetPathId pathId);

    public void Write(string outputPath, AssetMutationPlan plan);
}
