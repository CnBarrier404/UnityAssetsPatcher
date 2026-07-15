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
        IReadOnlyList<AssetsInfo> assets = _assetsReader.ReadAssetsInfo(request.AssetsFilePath);
        IEnumerable<AssetsInfo> listedAssets = request.Limit is null
            ? assets
            : assets.Take(request.Limit.Value);
        InspectAssetSummary[] summaries = listedAssets
            .Select(asset => new InspectAssetSummary(
                asset.PathId,
                asset.TypeName,
                ReadName(request.AssetsFilePath, asset.PathId)))
            .ToArray();

        return new InspectListResult(summaries, assets.Count);
    }

    public AssetsFieldInfo Fields(InspectFieldsRequest request)
    {
        return _assetsReader.ReadAssetsFieldInfo(request.AssetsFilePath, request.PathId);
    }

    private string? ReadName(string assetsFilePath, long pathId)
    {
        try
        {
            AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(assetsFilePath, pathId);
            return fieldTree.Child("m_Name")?.Value?.ToInvariantString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
