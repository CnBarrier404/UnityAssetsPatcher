namespace UnityAssetsPatcher.Abstractions.Assets;

public interface IAssetsAccessScope : IDisposable
{
    public IAssetsFileReader Reader { get; }
    public IAssetsFileWriter Writer { get; }
    public void CloseReadSessions();
}
