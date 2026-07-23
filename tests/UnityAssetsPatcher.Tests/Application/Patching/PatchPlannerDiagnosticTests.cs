using System.Text.Json;
using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Json;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Patching;

public sealed class PatchPlannerDiagnosticTests
{
    [Fact]
    public void Plan_WhenOperationIsMissing_ReturnsInvalidPatchConfiguration()
    {
        var assets = new StubAssetsFileService([]);
        PatchPlanner planner = CreatePlanner(assets);
        ManifestPatch patch = CreatePatch("Material", [], null);

        PatchPlanningResult result = Plan(planner, [patch]);

        AssertDiagnostic(result, PatchDiagnosticCode.InvalidPatchConfiguration);
    }

    [Fact]
    public void Plan_WhenReplacementAndFieldOperationsAreMixed_ReturnsInvalidPatchConfiguration()
    {
        var assets = new StubAssetsFileService([]);
        PatchPlanner planner = CreatePlanner(assets);
        ManifestPatch patch = CreatePatch(
            "Material",
            [new ManifestSetOperation("m_Value", JsonElementFactory.Number(1), JsonElementFactory.Number(2))],
            new ManifestReplaceFrom(SourcePath, "m_Name"));

        PatchPlanningResult result = Plan(planner, [patch]);

        AssertDiagnostic(result, PatchDiagnosticCode.InvalidPatchConfiguration);
        Assert.Equal(
            "Manifest 'replaceAsset' operations cannot be combined with 'set', 'add', or 'copyAsset' operations for the same assets file.",
            result.Diagnostic?.Detail);
    }

    [Fact]
    public void Plan_WhenNoAssetMatches_ReturnsNoMatchingAssets()
    {
        var assets = new StubAssetsFileService([]);
        PatchPlanner planner = CreatePlanner(assets);
        ManifestPatch patch = CreateScalarPatch(1, 2);

        PatchPlanningResult result = Plan(planner, [patch]);

        AssertDiagnostic(result, PatchDiagnosticCode.NoMatchingAssets);
    }

    [Fact]
    public void Plan_WhenObjectChildFieldIsMissing_ReturnsFieldNotFound()
    {
        var assets = new StubAssetsFileService(
            [new AssetInfo(1, "Material")],
            new Dictionary<long, AssetField>
            {
                [1] = new AssetField("Material", "Material", null,
                [
                    new AssetField("m_Settings", "Settings", null,
                    [
                        new AssetField("m_Existing", "SInt32", new AssetFieldValue.Int64(1), []),
                    ]),
                ]),
            });
        PatchPlanner planner = CreatePlanner(assets);
        var operation = new ManifestSetOperation(
            "m_Settings",
            JsonUtils.ParseElement("{}"),
            JsonUtils.ParseElement("{\"m_Missing\":2}"));

        PatchPlanningResult result = Plan(planner, [CreatePatch("Material", [operation], null)]);

        AssertDiagnostic(result, PatchDiagnosticCode.FieldNotFound);
    }

    [Fact]
    public void Plan_WhenCurrentValueDoesNotMatch_ReturnsValueMismatch()
    {
        PatchPlanner planner = CreatePlanner(CreateScalarAssets(1));
        ManifestPatch patch = CreateScalarPatch(2, 3);

        PatchPlanningResult result = Plan(planner, [patch]);

        AssertDiagnostic(result, PatchDiagnosticCode.ValueMismatch);
    }

    [Fact]
    public void Plan_WhenReplacementValueIsUnsupported_ReturnsUnsupportedValue()
    {
        PatchPlanner planner = CreatePlanner(CreateScalarAssets(1));
        var operation = new ManifestSetOperation(
            "m_Value",
            JsonElementFactory.Number(1),
            JsonUtils.ParseElement("null"));

        PatchPlanningResult result = Plan(planner, [CreatePatch("Material", [operation], null)]);

        AssertDiagnostic(result, PatchDiagnosticCode.UnsupportedValue);
    }

    [Fact]
    public void Plan_WhenPathIdReferenceDoesNotMatch_ReturnsPathIdReferenceNotFound()
    {
        PatchPlanner planner = CreatePlanner(CreatePathIdAssets(["Other"]));
        ManifestPatch patch = CreatePathIdPatch("Missing");

        PatchPlanningResult result = Plan(planner, [patch]);

        AssertDiagnostic(result, PatchDiagnosticCode.PathIdReferenceNotFound);
    }

    [Fact]
    public void Plan_WhenPathIdReferenceMatchesMultiple_ReturnsPathIdReferenceAmbiguous()
    {
        PatchPlanner planner = CreatePlanner(CreatePathIdAssets(["Duplicate", "Duplicate"]));
        ManifestPatch patch = CreatePathIdPatch("Duplicate");

        PatchPlanningResult result = Plan(planner, [patch]);

        AssertDiagnostic(result, PatchDiagnosticCode.PathIdReferenceAmbiguous);
    }

    [Fact]
    public void Plan_WhenReplacementSourcePathIsMissing_ReturnsReplacementSourceNotFound()
    {
        var assets = new StubAssetsFileService(
            [new AssetInfo(1, "AudioClip")],
            new Dictionary<long, AssetField>
            {
                [1] = CreateNamedField("AudioClip", "Target"),
            });
        PatchPlanner planner = CreatePlanner(assets);
        ManifestPatch patch = CreateReplacementPatch();

        PatchPlanningResult result = Plan(planner, [patch]);

        AssertDiagnostic(result, PatchDiagnosticCode.ReplacementSourceNotFound);
    }

    [Fact]
    public void Plan_WhenReplacementSourceDoesNotMatch_ReturnsReplacementMatchInvalid()
    {
        var assets = new StubAssetsFileService(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [AssetsPath] = [new AssetInfo(1, "AudioClip")],
                [SourcePath] = [],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetField>
            {
                [(AssetsPath, 1)] = CreateNamedField("AudioClip", "Target"),
            });
        PatchPlanner planner = CreatePlanner(assets);
        ManifestPatch patch = CreateReplacementPatch();

        PatchPlanningResult result = planner.Plan(new PatchPlanningRequest(
            AssetsPath,
            [patch],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SourcePath] = SourcePath,
            }));

        AssertDiagnostic(result, PatchDiagnosticCode.ReplacementMatchInvalid);
    }

    [Fact]
    public void Plan_WhenPreviewDetailsDiffer_ProducesSameWritePlan()
    {
        ManifestPatch patch = CreateScalarPatch(1, 2);
        PatchPlanningResult detailed = Plan(CreatePlanner(CreateScalarAssets(1)), [patch]);
        PatchPlanningResult summary = Plan(
            CreatePlanner(CreateScalarAssets(1)),
            [patch],
            includePreviewDetails: false);

        FieldPatchPlan detailedPlan = Assert.IsType<FieldPatchPlan>(detailed.Plan);
        FieldPatchPlan summaryPlan = Assert.IsType<FieldPatchPlan>(summary.Plan);
        Assert.Equal(
            SerializePlan(detailedPlan),
            SerializePlan(summaryPlan));
        Assert.Single(detailed.Preview.Assets);
        Assert.Empty(summary.Preview.Assets);
    }

    [Fact]
    public void Plan_WhenPreviewDetailsDiffer_ProducesSameDiagnostic()
    {
        ManifestPatch patch = CreateScalarPatch(2, 3);
        PatchPlanningResult detailed = Plan(CreatePlanner(CreateScalarAssets(1)), [patch]);
        PatchPlanningResult summary = Plan(
            CreatePlanner(CreateScalarAssets(1)),
            [patch],
            includePreviewDetails: false);

        Assert.Equal(detailed.Diagnostic, summary.Diagnostic);
        AssertDiagnostic(summary, PatchDiagnosticCode.ValueMismatch);
    }

    [Fact]
    public void Plan_WhenAssetsReaderThrowsIOException_PropagatesException()
    {
        PatchPlanner planner = CreatePlanner(new ThrowingAssetsFileReader());
        ManifestPatch patch = CreateScalarPatch(1, 2);

        var exception = Assert.Throws<IOException>(() => Plan(planner, [patch]));

        Assert.Equal("Read failed.", exception.Message);
    }

    private const string AssetsPath = "target.assets";
    private const string SourcePath = "source.assets";

    private static PatchPlanner CreatePlanner(IAssetsFileReader assetsReader)
    {
        var queryService = new AssetQueryService(assetsReader);
        var fieldPlanner = new FieldPatchPlanner(
            queryService,
            [new SetFieldPatchOperationHandler(), new AddFieldPatchOperationHandler()]);

        return new PatchPlanner(
            fieldPlanner,
            new ReplacementPlanner(queryService),
            new CopyAssetPlanner(queryService));
    }

    private static PatchPlanningResult Plan(
        PatchPlanner planner,
        IReadOnlyList<ManifestPatch> patches,
        bool includePreviewDetails = true)
    {
        return planner.Plan(new PatchPlanningRequest(AssetsPath, patches, new Dictionary<string, string>())
        {
            IncludePreviewDetails = includePreviewDetails,
        });
    }

    private static ManifestPatch CreateScalarPatch(long from, long to)
    {
        return CreatePatch(
            "Material",
            [new ManifestSetOperation("m_Value", JsonElementFactory.Number(from), JsonElementFactory.Number(to))],
            null);
    }

    private static ManifestPatch CreatePathIdPatch(string referencedName)
    {
        var operation = new ManifestSetOperation(
            "m_Reference",
            JsonElementFactory.Number(0),
            JsonUtils.ParseElement(
                $$"""
                  {
                    "$pathId": {
                      "type": "Texture2D",
                      "match": { "m_Name": "{{referencedName}}" }
                    }
                  }
                  """));

        return CreatePatch("Material", [operation], null);
    }

    private static ManifestPatch CreateReplacementPatch()
    {
        return CreatePatch(
            "AudioClip",
            null,
            new ManifestReplaceFrom(SourcePath, "m_Name"));
    }

    private static ManifestPatch CreatePatch(
        string assetType,
        IReadOnlyList<ManifestSetOperation>? setOperations,
        ManifestReplaceFrom? replaceFrom)
    {
        return new ManifestPatch(
            AssetsPath,
            assetType,
            new Dictionary<string, JsonElement>(),
            setOperations,
            null,
            replaceFrom);
    }

    private static StubAssetsFileService CreateScalarAssets(long value)
    {
        return new StubAssetsFileService(
            [new AssetInfo(1, "Material")],
            new Dictionary<long, AssetField>
            {
                [1] = new AssetField("Material", "Material", null,
                [
                    new AssetField("m_Value", "SInt64", new AssetFieldValue.Int64(value), []),
                ]),
            });
    }

    private static StubAssetsFileService CreatePathIdAssets(IReadOnlyList<string> referencedNames)
    {
        var assets = new List<AssetInfo> { new(1, "Material") };
        var fields = new Dictionary<long, AssetField>
        {
            [1] = new AssetField("Material", "Material", null,
            [
                new AssetField("m_Reference", "SInt64", new AssetFieldValue.Int64(0), []),
            ]),
        };

        for (int index = 0; index < referencedNames.Count; index++)
        {
            long pathId = index + 100;
            assets.Add(new AssetInfo(pathId, "Texture2D"));
            fields.Add(pathId, CreateNamedField("Texture2D", referencedNames[index]));
        }

        return new StubAssetsFileService(assets, fields);
    }

    private static AssetField CreateNamedField(string typeName, string name)
    {
        return new AssetField(
            typeName,
            typeName,
            null,
            [new AssetField("m_Name", "string", new AssetFieldValue.String(name), [])]);
    }

    private static string SerializePlan(FieldPatchPlan plan)
    {
        return JsonSerializer.Serialize(plan);
    }

    private static void AssertDiagnostic(PatchPlanningResult result, PatchDiagnosticCode expectedCode)
    {
        Assert.False(result.CanApply);
        Assert.Null(result.Plan);
        Assert.Equal(expectedCode, result.Diagnostic?.Code);
        Assert.Equal(AssetsPath, result.Diagnostic?.AssetsFilePath);
        Assert.Same(result.Diagnostic, result.Preview.Diagnostic);
    }

    private sealed class ThrowingAssetsFileReader : IAssetsFileReader
    {
        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            throw new IOException("Read failed.");
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            throw new NotSupportedException();
        }

        public void Dispose() { }
    }
}
