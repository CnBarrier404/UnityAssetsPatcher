using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Features.Inspect;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Features.Inspect;

public sealed class InspectAssetsHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenListingAssets_ReturnsLimitedSummariesAndNames()
    {
        const string assetsFilePath = "sharedassets0.assets";
        AssetInfo[] assets =
        [
            new(new AssetPathId(1), "Type One"),
            new(new AssetPathId(2), "Type Two"),
            new(new AssetPathId(3), "Type Three")
        ];
        AssetField firstField = new AssetObjectField(
            "Root",
            "Type One",
            [new AssetScalarField("m_Name", "string", new AssetScalarValue.String("First"))]);
        var reader = new StubAssetsFileReader(
            _ => assets,
            (_, pathId) => pathId == 1
                ? firstField
                : throw new InvalidOperationException("Name is unavailable."));
        var handler = new InspectAssetsHandler(reader);

        var result = await handler.HandleAsync(
            new InspectListRequest(assetsFilePath, 2),
            TestContext.Current.CancellationToken);

        InspectListResult value = Assert.IsType<OperationSucceeded<InspectListResult>>(result).Value;
        Assert.Equal(3, value.TotalCount);
        Assert.Collection(
            value.Assets,
            first =>
            {
                Assert.Equal(1L, first.PathId);
                Assert.Equal("Type One", first.TypeName);
                Assert.Equal("First", first.Name);
            },
            second =>
            {
                Assert.Equal(2L, second.PathId);
                Assert.Equal("Type Two", second.TypeName);
                Assert.Null(second.Name);
            });
    }

    [Fact]
    public async Task HandleAsync_WhenFieldsAreRequested_ReturnsFieldTree()
    {
        AssetField expected = new AssetScalarField(
            "m_Name",
            "string",
            new AssetScalarValue.String("Test"));
        var reader = new StubAssetsFileReader(
            _ => [],
            (_, _) => expected);
        var handler = new InspectAssetsHandler(reader);

        var result = await handler.HandleAsync(
            new InspectFieldsRequest("sharedassets0.assets", 1),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, Assert.IsType<OperationSucceeded<AssetField>>(result).Value);
    }

    [Fact]
    public async Task HandleAsync_WhenFieldIsMissing_ReturnsAssetNotFound()
    {
        var reader = new StubAssetsFileReader(
            _ => [],
            (_, _) => throw new InvalidOperationException("Asset was not found."));
        var handler = new InspectAssetsHandler(reader);

        var result = await handler.HandleAsync(
            new InspectFieldsRequest("sharedassets0.assets", 1),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<AssetField>>(result);
        Assert.Equal(AssetErrorCodes.NotFound, failure.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenAssetsFileDoesNotExist_ReturnsFileNotFound()
    {
        const string assetsFilePath = "missing.assets";
        var reader = new StubAssetsFileReader(
            path => throw new FileNotFoundException("Assets file was not found.", path),
            (_, _) => throw new NotSupportedException());
        var handler = new InspectAssetsHandler(reader);

        var result = await handler.HandleAsync(
            new InspectListRequest(assetsFilePath, null),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<InspectListResult>>(result);
        Assert.Equal(FileErrorCodes.NotFound, failure.Error.Code);
        Assert.Equal(assetsFilePath, failure.Error.Parameters["path"]);
    }

    private sealed class StubAssetsFileReader : IAssetsFileReader
    {
        private readonly Func<string, IReadOnlyList<AssetInfo>> _readAssets;
        private readonly Func<string, long, AssetField> _readField;

        public StubAssetsFileReader(
            Func<string, IReadOnlyList<AssetInfo>> readAssets,
            Func<string, long, AssetField> readField)
        {
            _readAssets = readAssets;
            _readField = readField;
        }

        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            return _readAssets(assetsFilePath);
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            return _readField(assetsFilePath, pathId);
        }
    }
}
