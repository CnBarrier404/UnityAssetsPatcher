using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScopeFactory : IAssetsAccessScopeFactory
{
    private readonly string _tpkFilePath;

    public AssetsToolsAccessScopeFactory(string tpkFilePath)
    {
        _tpkFilePath = tpkFilePath;
    }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(new AssetsFileReader(_tpkFilePath), new AssetsFileWriter(_tpkFilePath));
    }
}
