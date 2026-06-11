using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class InspectAssetsWorkflowTests
{
    [Fact]
    public void List_ReturnsAssetMetadataFromReader()
    {
        var assets = new[]
        {
            new AssetsInfo(1, 20, "Camera", 128),
            new AssetsInfo(2, 108, "Light", 96),
        };
        var workflow = new InspectAssetsWorkflow(new StubAssetsFileService(assets));

        var result = workflow.List(new InspectListRequest("resources.assets", 100));

        Assert.Equal(assets, result.Assets);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public void List_WhenLimitIsProvided_ReturnsLimitedAssetsAndTotalCount()
    {
        var assets = Enumerable.Range(1, 5)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        var workflow = new InspectAssetsWorkflow(new StubAssetsFileService(assets));

        var result = workflow.List(new InspectListRequest("resources.assets", 2));

        Assert.Equal(2, result.Assets.Count);
        Assert.Equal([assets[0], assets[1]], result.Assets);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public void List_WhenLimitIsNull_ReturnsAllAssetsAndTotalCount()
    {
        var assets = Enumerable.Range(1, 5)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        var workflow = new InspectAssetsWorkflow(new StubAssetsFileService(assets));

        var result = workflow.List(new InspectListRequest("resources.assets", null));

        Assert.Equal(assets, result.Assets);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public void Fields_ReturnsSelectedAssetFieldTree()
    {
        AssetsFieldInfo fieldTree = new(
            "Camera",
            "Camera",
            null,
            [new AssetsFieldInfo("field of view", "float", "90.0", [])]);
        var reader = new StubAssetsFileService(
            [],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = fieldTree,
            });
        var workflow = new InspectAssetsWorkflow(reader);

        AssetsFieldInfo result = workflow.Fields(new InspectFieldsRequest("resources.assets", 4));

        Assert.Same(fieldTree, result);
        Assert.Equal(4, reader.ReceivedPathId);
    }
}
