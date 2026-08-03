namespace UnityAssetsPatcher.Application.Assets;

public interface IAssetFileSessionFactory
{
    public IAssetFileSession Open(string inputPath);
}
