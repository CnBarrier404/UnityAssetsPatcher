using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallAssetsReadResources
{
    private readonly IAssetsAccessScope _assets;

    public InstallAssetsReadResources(IAssetsAccessScope assets)
    {
        _assets = assets;
    }

    public void Release()
    {
        _assets.ReleaseReadResources();
    }
}
