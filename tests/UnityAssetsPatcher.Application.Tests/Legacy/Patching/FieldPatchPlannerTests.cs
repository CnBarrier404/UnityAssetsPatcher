using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Json;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Patching;

public sealed class FieldPatchPlannerTests
{
    [Fact]
    public void PatchPlanner_WhenPlanSucceeds_ProducesPreviewAndWritePlanFromOneQuerySession()
    {
        TestScenario scenario = CreateSharedResolverScenario();
        var planner = new PatchPlanner(
            scenario.Builder,
            new ReplacementPlanner(new AssetQueryService(scenario.Reader)));

        PatchPlanningResult result = planner.Plan(new PatchPlanningRequest(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Referenced")])],
            new Dictionary<string, string>()));

        Assert.True(result.CanApply);
        Assert.Null(result.Diagnostic);
        Assert.Equal(2, result.Preview.Assets.Count);
        Assert.Equal(2, Assert.IsType<FieldPatchPlan>(result.Plan).Assets.Count);
        AssertAllAssetsReadOnce(scenario.Reader);
    }

    [Fact]
    public void PatchPlanner_WhenPreviewDetailsAreDisabled_ProducesWritePlanWithoutOperationDetails()
    {
        TestScenario scenario = CreateSharedResolverScenario();
        var planner = new PatchPlanner(
            scenario.Builder,
            new ReplacementPlanner(new AssetQueryService(scenario.Reader)));

        PatchPlanningResult result = planner.Plan(new PatchPlanningRequest(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Referenced")])],
            new Dictionary<string, string>())
        {
            IncludePreviewDetails = false,
        });

        Assert.True(result.CanApply);
        Assert.Empty(result.Preview.Assets);
        Assert.Equal(2, Assert.IsType<FieldPatchPlan>(result.Plan).Assets.Count);
        AssertAllAssetsReadOnce(scenario.Reader);
    }

    [Fact]
    public void PatchPlanner_WhenPathIdResolverDoesNotMatch_ReturnsStructuredDiagnostic()
    {
        var reader = new CountingAssetsFileReader(
            [new AssetInfo(1, "Material"), new AssetInfo(101, "Texture2D")],
            new Dictionary<long, AssetField>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Other"),
            });
        var queryService = new AssetQueryService(reader);
        var planner = new PatchPlanner(
            CreateFieldPatchPlanner(queryService),
            new ReplacementPlanner(queryService));

        PatchPlanningResult result = planner.Plan(new PatchPlanningRequest(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Missing")])],
            new Dictionary<string, string>()));

        Assert.False(result.CanApply);
        Assert.Null(result.Plan);
        Assert.Equal(PatchDiagnosticCode.PathIdReferenceNotFound, result.Diagnostic?.Code);
        Assert.Same(result.Diagnostic, result.Preview.Diagnostic);
    }

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
    public void CreateWritePlan_WhenPatchHasMultipleResolvers_DoesNotRetainResolverCandidateTrees()
    {
        var reader = new CountingAssetsFileReader(
            [
                new AssetInfo(1, "Material"),
                new AssetInfo(101, "Texture2D"),
                new AssetInfo(102, "Texture2D"),
            ],
            new Dictionary<long, AssetField>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_First", 0), ("m_Second", 0)),
                [101] = CreateFieldTree("Texture2D", "First"),
                [102] = CreateFieldTree("Texture2D", "Second"),
            });
        FieldPatchPlanner builder = CreateFieldPatchPlanner(new AssetQueryService(reader));

        AssetFieldPatch assetPatch = Assert.Single(builder.CreateWritePlan(
            AssetsPath,
            [
                CreatePatch([
                    CreateSetOperation("m_First", "Texture2D", "First"),
                    CreateSetOperation("m_Second", "Texture2D", "Second"),
                ])
            ]));

        Assert.Equal([101L, 102L], assetPatch.Operations.Select(operation => operation.To.GetInt64()));
        Assert.Equal(1, reader.AssetsReadCount);
        Assert.Equal(1, reader.GetFieldReadCount(1));
        Assert.Equal(2, reader.GetFieldReadCount(101));
        Assert.Equal(2, reader.GetFieldReadCount(102));
    }

    [Fact]
    public void CreatePreview_WhenTargetDoesNotMatch_DoesNotResolvePathId()
    {
        var reader = new CountingAssetsFileReader(
            [new AssetInfo(1, "Material"), new AssetInfo(101, "Texture2D")],
            new Dictionary<long, AssetField>
            {
                [1] = CreateFieldTree("Material", "Other", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Referenced"),
            });
        FieldPatchPlanner builder = CreateFieldPatchPlanner(new AssetQueryService(reader));

        PatchPreviewResult preview = builder.CreatePreview(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Referenced")])]);

        Assert.Empty(preview.Assets);
        Assert.Equal(1, reader.GetFieldReadCount(1));
        Assert.Equal(0, reader.GetFieldReadCount(101));
    }

    [Fact]
    public void CreateWritePlan_WhenPatchesShareAssetType_ReadsEachCandidateOnce()
    {
        var reader = new CountingAssetsFileReader(
            [
                new AssetInfo(1, "Material"),
                new AssetInfo(2, "Material"),
                new AssetInfo(3, "Material"),
            ],
            new Dictionary<long, AssetField>
            {
                [1] = CreateFieldTree("Material", "First", ("m_Value", 0)),
                [2] = CreateFieldTree("Material", "Second", ("m_Value", 0)),
                [3] = CreateFieldTree("Material", "Other", ("m_Value", 0)),
            });
        FieldPatchPlanner builder = CreateFieldPatchPlanner(new AssetQueryService(reader));
        ModSetOperation operation = new(
            "m_Value",
            JsonElementFactory.Number(0),
            JsonElementFactory.Number(1));

        IReadOnlyList<AssetFieldPatch> plan = builder.CreateWritePlan(
            AssetsPath,
            [CreatePatch([operation], "First"), CreatePatch([operation], "Second")]);

        Assert.Equal([1L, 2L], plan.Select(asset => asset.PathId));
        Assert.Equal(3, reader.TotalFieldReadCount);
        Assert.All(reader.FieldReadCounts.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    public void CreateWritePlan_WhenPathIdResolverDoesNotMatch_ThrowsExistingError()
    {
        var reader = new CountingAssetsFileReader(
            [new AssetInfo(1, "Material"), new AssetInfo(101, "Texture2D")],
            new Dictionary<long, AssetField>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Other"),
            });
        FieldPatchPlanner builder = CreateFieldPatchPlanner(new AssetQueryService(reader));

        var exception = Assert.Throws<PatchPlanningException>(() => builder.CreateWritePlan(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Missing")])]));

        Assert.Equal(PatchDiagnosticCode.PathIdReferenceNotFound, exception.Diagnostic.Code);
        Assert.Equal(
            "Path ID reference did not match any assets for type 'Texture2D'.",
            exception.Message);
    }

    [Fact]
    public void CreateWritePlan_WhenPathIdResolverMatchesMultipleAssets_ThrowsExistingError()
    {
        var reader = new CountingAssetsFileReader(
            [
                new AssetInfo(1, "Material"),
                new AssetInfo(101, "Texture2D"),
                new AssetInfo(102, "Texture2D"),
            ],
            new Dictionary<long, AssetField>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Duplicate"),
                [102] = CreateFieldTree("Texture2D", "Duplicate"),
            });
        FieldPatchPlanner builder = CreateFieldPatchPlanner(new AssetQueryService(reader));

        var exception = Assert.Throws<PatchPlanningException>(() => builder.CreateWritePlan(
            AssetsPath,
            [CreatePatch([CreateSetOperation("m_Reference", "Texture2D", "Duplicate")])]));

        Assert.Equal(PatchDiagnosticCode.PathIdReferenceAmbiguous, exception.Diagnostic.Code);
        Assert.Equal(
            "Path ID reference matched multiple assets for type 'Texture2D'.",
            exception.Message);
    }

    private const string AssetsPath = "sharedassets0.assets";

    private static TestScenario CreateSharedResolverScenario()
    {
        var reader = new CountingAssetsFileReader(
            [
                new AssetInfo(1, "Material"),
                new AssetInfo(2, "Material"),
                new AssetInfo(101, "Texture2D"),
                new AssetInfo(102, "Texture2D"),
            ],
            new Dictionary<long, AssetField>
            {
                [1] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [2] = CreateFieldTree("Material", "Target", ("m_Reference", 0)),
                [101] = CreateFieldTree("Texture2D", "Referenced"),
                [102] = CreateFieldTree("Texture2D", "Other"),
            });

        return new TestScenario(reader, CreateFieldPatchPlanner(new AssetQueryService(reader)));
    }

    private static FieldPatchPlanner CreateFieldPatchPlanner(AssetQueryService queryService) =>
        new(queryService, [new SetFieldPatchOperationHandler(), new AddFieldPatchOperationHandler()]);

    private static ModPatch CreatePatch(
        IReadOnlyList<ModSetOperation> setOperations,
        string assetName = "Target")
    {
        return new ModPatch(
            AssetsPath,
            "Material",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["m_Name"] = JsonElementFactory.String(assetName),
            },
            setOperations,
            [],
            null,
            null,
            null);
    }

    private static ModSetOperation CreateSetOperation(
        string fieldPath,
        string assetTypeName,
        string assetName)
    {
        return new ModSetOperation(
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

    private static AssetField CreateFieldTree(
        string assetTypeName,
        string name,
        params (string Name, long Value)[] fields)
    {
        return TestAssetField.Create(
            assetTypeName,
            assetTypeName,
            null,
            [
                TestAssetField.Create("m_Name", "string", new AssetFieldValue.String(name), []),
                .. fields.Select(field =>
                    TestAssetField.Create(field.Name, "SInt64", new AssetFieldValue.Int64(field.Value), [])),
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
        FieldPatchPlanner Builder);

    private sealed class CountingAssetsFileReader : IAssetsFileReader
    {
        private readonly IReadOnlyList<AssetInfo> _assets;
        private readonly IReadOnlyDictionary<long, AssetField> _fieldsByPathId;
        private readonly Dictionary<long, int> _fieldReadCounts = new();

        public int AssetsReadCount { get; private set; }
        public int AssetCount => _assets.Count;
        public IReadOnlyDictionary<long, int> FieldReadCounts => _fieldReadCounts;
        public int TotalFieldReadCount => _fieldReadCounts.Values.Sum();

        public CountingAssetsFileReader(
            IReadOnlyList<AssetInfo> assets,
            IReadOnlyDictionary<long, AssetField> fieldsByPathId)
        {
            _assets = assets;
            _fieldsByPathId = fieldsByPathId;
        }

        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            AssetsReadCount++;
            return _assets;
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            _fieldReadCounts[pathId] = GetFieldReadCount(pathId) + 1;
            return _fieldsByPathId[pathId];
        }

        public void Dispose() { }

        public int GetFieldReadCount(long pathId)
        {
            return _fieldReadCounts.GetValueOrDefault(pathId);
        }
    }
}
