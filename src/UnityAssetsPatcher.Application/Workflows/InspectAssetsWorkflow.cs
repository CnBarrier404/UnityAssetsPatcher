using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InspectAssetsWorkflow
{
    private readonly IAssetsFileReader _assetsReader;
    private readonly ILogger<InspectAssetsWorkflow> _logger;

    public InspectAssetsWorkflow(
        IAssetsFileReader assetsReader,
        ILogger<InspectAssetsWorkflow>? logger = null)
    {
        _assetsReader = assetsReader;
        _logger = logger ?? NullLogger<InspectAssetsWorkflow>.Instance;
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

        _logger.LogInformation(
            "Inspected {AssetsFilePath}: {ListedAssetCount} of {TotalAssetCount} assets listed",
            request.AssetsFilePath,
            summaries.Length,
            assets.Count);

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
