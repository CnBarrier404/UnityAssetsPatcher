namespace UnityAssetsPatcher.Core;

public interface IAssetsReader
{
    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath);

    public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId);
}
