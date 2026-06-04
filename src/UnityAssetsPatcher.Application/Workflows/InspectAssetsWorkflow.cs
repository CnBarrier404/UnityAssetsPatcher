using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InspectAssetsWorkflow
{
    private readonly IAssetsReader _assetsReader;

    public InspectAssetsWorkflow(IAssetsReader assetsReader)
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
