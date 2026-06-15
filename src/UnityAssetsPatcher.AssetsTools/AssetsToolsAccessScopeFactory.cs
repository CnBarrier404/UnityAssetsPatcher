using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScopeFactory : IAssetsAccessScopeFactory, IDisposable
{
    public AssetsToolsAccessScopeFactory(string tpkFilePath)
    {
        Context = new AssetsToolsContext(tpkFilePath);
    }

    public AssetsToolsContext Context { get; }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(new AssetsFileReader(Context), new AssetsFileWriter(Context));
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
