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

    public IReadOnlyList<AssetsInfo> List(InspectListRequest request)
    {
        return _assetsReader.ReadAssetsInfo(request.AssetsFilePath);
    }

    public AssetsFieldInfo Fields(InspectFieldsRequest request)
    {
        return _assetsReader.ReadAssetsFieldInfo(request.AssetsFilePath, request.PathId);
    }
}
