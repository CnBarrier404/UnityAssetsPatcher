namespace UnityAssetsPatcher.Application.Assets;

public interface IAssetsFileReader
{
    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath);
    public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId);
    public void CloseReadSessions();
}
