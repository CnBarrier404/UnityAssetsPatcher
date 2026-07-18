using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Assets;

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
        IReadOnlyList<AssetInfo> assets = _assetsReader.ReadAssets(request.AssetsFilePath);
        IEnumerable<AssetInfo> listedAssets = request.Limit is null
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

    public AssetField Fields(InspectFieldsRequest request)
    {
        return _assetsReader.ReadField(request.AssetsFilePath, request.PathId);
    }

    private string? ReadName(string assetsFilePath, long pathId)
    {
        try
        {
            AssetField fieldTree = _assetsReader.ReadField(assetsFilePath, pathId);

            return fieldTree.FindChild("m_Name")?.Value?.ToInvariantString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
