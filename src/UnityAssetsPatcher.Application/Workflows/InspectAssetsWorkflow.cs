using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InspectAssetsWorkflow
{
    private readonly IAssetsFileReader _assetsReader;

    public InspectAssetsWorkflow(IAssetsFileReader assetsReader)
    {
        _assetsReader = assetsReader;
    }

    public InspectListResult List(InspectListRequest request)
    {
        var assets = _assetsReader.ReadAssetsInfo(request.AssetsFilePath);
        var listedAssets = request.Limit is null
            ? assets
            : assets.Take(request.Limit.Value).ToArray();

        return new InspectListResult(listedAssets, assets.Count);
    }

    public AssetsFieldInfo Fields(InspectFieldsRequest request)
    {
        return _assetsReader.ReadAssetsFieldInfo(request.AssetsFilePath, request.PathId);
    }
}
