namespace UnityAssetsPatcher.Core.Assets;

public interface IAssetsAccessScope : IDisposable
{
    IAssetsFileReader Reader { get; }
    IAssetsFileWriter Writer { get; }
    void ReleaseReadResources();
}
