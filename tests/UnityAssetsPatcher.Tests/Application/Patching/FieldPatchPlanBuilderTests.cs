using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Patching;

public sealed class FieldPatchPlanBuilderTests
{
    [Fact]
    public void CreateWritePlan_WhenMatchesSharePathIdResolver_ReadsEachAssetOnce()
    {
        TestScenario scenario = CreateSharedResolverScenario();

        var plan = scenario.Builder.CreateWritePlan(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Referenced")])]);

        Assert.Equal(2, plan.Count);
        Assert.All(plan, assetPatch =>
        {
            FieldPatchOperation operation = Assert.Single(assetPatch.Operations);
            Assert.Equal(101, operation.To.GetInt64());
        });
        AssertAllAssetsReadOnce(scenario.Reader);
    }

    [Fact]
    public void CreatePreview_WhenMatchesSharePathIdResolver_ReadsEachAssetOnce()
    {
        TestScenario scenario = CreateSharedResolverScenario();

        PatchPreviewResult preview = scenario.Builder.CreatePreview(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Referenced")])]);

        Assert.Equal(2, preview.Assets.Count);
        Assert.All(preview.Assets, asset =>
            Assert.Equal("101", Assert.Single(asset.Operations).ToText));
        AssertAllAssetsReadOnce(scenario.Reader);
    }

    [Fact]
    public void CreateWritePlan_WhenPatchHasMultipleResolvers_ReusesQueryContext()
    {
        var reader = new CountingAssetsFileReader(
            [
                new AssetsInfo(1, "Material"),
                new AssetsInfo(101, "Texture2D"),
                new AssetsInfo(102, "Texture2D"),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_First", 0), ("m_Second", 0)),
                [101] = CreateFieldTree("Texture2D", "First"),
                [102] = CreateFieldTree("Texture2D", "Second"),
            });
        var builder = new FieldPatchPlanBuilder(new AssetQueryService(reader));

        AssetFieldPatch assetPatch = Assert.Single(builder.CreateWritePlan(
            AssetsPath,
            [
                CreatePatch([
                    CreateSetOperation("m_First", "Texture2D", "First"),
                    CreateSetOperation("m_Second", "Texture2D", "Second"),
                ])
            ]));

        Assert.Equal([101L, 102L], assetPatch.Operations.Select(operation => operation.To.GetInt64()));
        AssertAllAssetsReadOnce(reader);
    }

    [Fact]
    public void CreatePreview_WhenTargetDoesNotMatch_DoesNotResolvePathId()
    {
        var reader = new CountingAssetsFileReader(
            [new AssetsInfo(1, "Material"), new AssetsInfo(101, "Texture2D")],
            new Dictionary<long, AssetsFieldInfo>
            {
                [1] = CreateFieldTree("Material", "Other", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Referenced"),
            });
        var builder = new FieldPatchPlanBuilder(new AssetQueryService(reader));

        PatchPreviewResult preview = builder.CreatePreview(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Referenced")])]);

        Assert.Empty(preview.Assets);
        Assert.Equal(1, reader.GetFieldReadCount(1));
        Assert.Equal(0, reader.GetFieldReadCount(101));
    }

    [Fact]
    public void CreateWritePlan_WhenPathIdResolverDoesNotMatch_ThrowsExistingError()
    {
        var reader = new CountingAssetsFileReader(
            [new AssetsInfo(1, "Material"), new AssetsInfo(101, "Texture2D")],
            new Dictionary<long, AssetsFieldInfo>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Other"),
            });
        var builder = new FieldPatchPlanBuilder(new AssetQueryService(reader));

        var exception = Assert.Throws<InvalidOperationException>(() => builder.CreateWritePlan(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Missing")])]));

        Assert.Equal(
            "Path ID reference did not match any assets for type 'Texture2D'.",
            exception.Message);
    }

    [Fact]
    public void CreateWritePlan_WhenPathIdResolverMatchesMultipleAssets_ThrowsExistingError()
    {
        var reader = new CountingAssetsFileReader(
            [
                new AssetsInfo(1, "Material"),
                new AssetsInfo(101, "Texture2D"),
                new AssetsInfo(102, "Texture2D"),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Duplicate"),
                [102] = CreateFieldTree("Texture2D", "Duplicate"),
            });
        var builder = new FieldPatchPlanBuilder(new AssetQueryService(reader));

        var exception = Assert.Throws<InvalidOperationException>(() => builder.CreateWritePlan(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Duplicate")])]));

        Assert.Equal(
            "Path ID reference matched multiple assets for type 'Texture2D'.",
            exception.Message);
    }

    private const string AssetsPath = "sharedassets0.assets";

    private static TestScenario CreateSharedResolverScenario()
    {
        var reader = new CountingAssetsFileReader(
            [
                new AssetsInfo(1, "Material"),
                new AssetsInfo(2, "Material"),
                new AssetsInfo(101, "Texture2D"),
                new AssetsInfo(102, "Texture2D"),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [2] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Referenced"),
                [102] = CreateFieldTree("Texture2D", "Other"),
            });

        return new TestScenario(reader, new FieldPatchPlanBuilder(new AssetQueryService(reader)));
    }

    private static ManifestPatch CreatePatch(IReadOnlyList<ManifestSetOperation> setOperations)
    {
        return new ManifestPatch(
            AssetsPath,
            "Material",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["m_Name"] = JsonElementFactory.String("Target"),
            },
            setOperations,
            null);
    }

    private static ManifestSetOperation CreateSetOperation(
        string fieldPath,
        string assetTypeName,
        string assetName)
    {
        return new ManifestSetOperation(
            fieldPath,
            JsonElementFactory.Number(0),
            CreatePathIdResolver(assetTypeName, assetName));
    }

    private static JsonElement CreatePathIdResolver(string assetTypeName, string assetName)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("$pathId");
            writer.WriteStartObject();
            writer.WriteString("type", assetTypeName);
            writer.WritePropertyName("match");
            writer.WriteStartObject();
            writer.WriteString("m_Name", assetName);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static AssetsFieldInfo CreateFieldTree(
        string assetTypeName,
        string name,
        params (string Name, long Value)[] fields)
    {
        return new AssetsFieldInfo(
            assetTypeName,
            assetTypeName,
            null,
            [
                new AssetsFieldInfo("m_Name", "string", name, []),
                .. fields.Select(field =>
                    new AssetsFieldInfo(field.Name, "SInt64", field.Value.ToString(), [])),
            ]);
    }

    private static void AssertAllAssetsReadOnce(CountingAssetsFileReader reader)
    {
        Assert.Equal(1, reader.AssetsReadCount);
        Assert.Equal(reader.AssetCount, reader.TotalFieldReadCount);
        Assert.All(reader.FieldReadCounts.Values, count => Assert.Equal(1, count));
    }

    private sealed record TestScenario(
        CountingAssetsFileReader Reader,
        FieldPatchPlanBuilder Builder);

    private sealed class CountingAssetsFileReader : IAssetsFileReader
    {
        private readonly IReadOnlyList<AssetsInfo> _assets;
        private readonly IReadOnlyDictionary<long, AssetsFieldInfo> _fieldsByPathId;
        private readonly Dictionary<long, int> _fieldReadCounts = new();

        public int AssetsReadCount { get; private set; }
        public int AssetCount => _assets.Count;
        public IReadOnlyDictionary<long, int> FieldReadCounts => _fieldReadCounts;
        public int TotalFieldReadCount => _fieldReadCounts.Values.Sum();

        public CountingAssetsFileReader(
            IReadOnlyList<AssetsInfo> assets,
            IReadOnlyDictionary<long, AssetsFieldInfo> fieldsByPathId)
        {
            _assets = assets;
            _fieldsByPathId = fieldsByPathId;
        }

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            AssetsReadCount++;
            return _assets;
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            _fieldReadCounts[pathId] = GetFieldReadCount(pathId) + 1;
            return _fieldsByPathId[pathId];
        }

        public void CloseReadSessions() { }

        public int GetFieldReadCount(long pathId)
        {
            return _fieldReadCounts.GetValueOrDefault(pathId);
        }
    }
}
