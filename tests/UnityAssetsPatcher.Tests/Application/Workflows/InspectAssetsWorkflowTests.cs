using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class InspectAssetsWorkflowTests
{
    [Fact]
    public void ListReturnsLimitedAssetsAndTotalCount()
    {
        AssetInfo[] assets = Enumerable.Range(1, 5)
            .Select(id => new AssetInfo(id, $"Asset{id}"))
            .ToArray();
        var workflow = new InspectAssetsWorkflow(CreateReader(assets));

        InspectListResult result = workflow.List(new InspectListRequest("resources.assets", 2));

        Assert.Equal(
            [
                new InspectAssetSummary(1, "Asset1", "Name1"),
                new InspectAssetSummary(2, "Asset2", "Name2"),
            ],
            result.Assets);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public void ListWithoutLimitReturnsEveryAsset()
    {
        AssetInfo[] assets = Enumerable.Range(1, 3)
            .Select(id => new AssetInfo(id, $"Asset{id}"))
            .ToArray();
        var workflow = new InspectAssetsWorkflow(CreateReader(assets));

        InspectListResult result = workflow.List(new InspectListRequest("resources.assets", null));

        Assert.Equal(
            assets.Select(asset => new InspectAssetSummary(
                asset.PathId,
                asset.TypeName,
                $"Name{asset.PathId}")),
            result.Assets);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public void FieldsReturnsSelectedAssetFieldTree()
    {
        var fieldTree = new AssetField(
            "Camera",
            "Camera",
            null,
            [new AssetField("field of view", "float", new AssetFieldValue.Float(90f), [])]);
        var workflow = new InspectAssetsWorkflow(new StubAssetsFileService(
            [],
            new Dictionary<long, AssetField> { [4] = fieldTree }));

        AssetField result = workflow.Fields(new InspectFieldsRequest("resources.assets", 4));

        Assert.Same(fieldTree, result);
    }

    [Fact]
    public void ListUsesEmptyNameWhenAssetHasNoReadableName()
    {
        var workflow = new InspectAssetsWorkflow(new StubAssetsFileService([new AssetInfo(1, "PreloadData")]));

        InspectListResult result = workflow.List(new InspectListRequest("resources.assets", null));

        Assert.Null(Assert.Single(result.Assets).Name);
    }

    private static StubAssetsFileService CreateReader(IReadOnlyList<AssetInfo> assets)
    {
        return new StubAssetsFileService(
            assets,
            assets.ToDictionary(
                asset => asset.PathId,
                asset => new AssetField(
                    "Base",
                    asset.TypeName,
                    null,
                    [new AssetField("m_Name", "string", new AssetFieldValue.String($"Name{asset.PathId}"), [])])));
    }
}
