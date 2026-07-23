using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Patching;

public sealed class CopyAssetPlannerTests
{
    [Fact]
    public void Plan_WhenSourceAndTargetAreUnique_CreatesCopyPlan()
    {
        StubAssetsFileService assets = CreateAssets();
        var planner = new CopyAssetPlanner(new AssetQueryService(assets));

        CopyAssetPlanningOutput result = planner.Plan("sharedassets6.assets", [CreateCopyPatch("Chair", "Table")]);

        AssetCopy copy = Assert.Single(result.Copies);
        Assert.Equal(82, copy.SourcePathId);
        Assert.Equal(79, copy.TargetPathId);
        PatchPreviewAssetResult preview = Assert.Single(result.Preview.Assets);
        Assert.Equal(79, preview.Asset.PathId);
        Assert.Equal("$copyAsset", Assert.Single(preview.Operations).Path);
    }

    [Fact]
    public void Plan_WhenCopiesAreChained_ThrowsClearError()
    {
        StubAssetsFileService assets = CreateAssets();
        var planner = new CopyAssetPlanner(new AssetQueryService(assets));

        var exception = Assert.Throws<PatchPlanningException>(() => planner.Plan(
            "sharedassets6.assets",
            [CreateCopyPatch("Chair", "Table"), CreateCopyPatch("Table", "Other")]));

        Assert.Equal(PatchDiagnosticCode.InvalidPatchConfiguration, exception.Diagnostic.Code);
        Assert.Contains("Chained or cyclic", exception.Message);
    }

    private static StubAssetsFileService CreateAssets()
    {
        return new StubAssetsFileService(
            [new AssetInfo(79, "Material"), new AssetInfo(82, "Material"), new AssetInfo(83, "Material")],
            new Dictionary<long, AssetField>
            {
                [79] = CreateField("Chair"),
                [82] = CreateField("Table"),
                [83] = CreateField("Other"),
            });
    }

    private static ManifestPatch CreateCopyPatch(string targetName, string sourceName)
    {
        return new ManifestPatch(
            "sharedassets6.assets",
            "Material",
            new Dictionary<string, JsonElement> { ["m_Name"] = JsonString(targetName) },
            null,
            null,
            CopyAssetFrom: new ManifestCopyAssetFrom(
                "Material",
                new Dictionary<string, JsonElement> { ["m_Name"] = JsonString(sourceName) }));
    }

    private static AssetField CreateField(string name)
    {
        return new AssetField(
            "Base",
            "Material",
            null,
            [new AssetField("m_Name", "string", new AssetFieldValue.String(name), [])]);
    }

    private static JsonElement JsonString(string value)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(value));

        return document.RootElement.Clone();
    }
}
