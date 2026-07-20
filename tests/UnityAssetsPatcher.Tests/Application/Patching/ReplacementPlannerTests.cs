using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Json;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Patching;

public sealed class ReplacementPlannerTests
{
    [Fact]
    public void CreateWritePlan_WhenPatchesShareSourceIndex_ReusesIndexWithoutRetainingTargetTrees()
    {
        var scenario = CreateTwoAssetScenario();

        IReadOnlyList<AssetReplacement> plan = scenario.Builder.CreateWritePlan(
            TargetPath,
            [CreatePatch("A"), CreatePatch("B")],
            SourcePaths);

        Assert.Equal(2, plan.Count);
        Assert.Equal((101L, 1L), (plan[0].SourcePathId, plan[0].TargetPathId));
        Assert.Equal((102L, 2L), (plan[1].SourcePathId, plan[1].TargetPathId));
        Assert.Equal(2, scenario.Reader.GetFieldReadCount(TargetPath, 1));
        Assert.Equal(2, scenario.Reader.GetFieldReadCount(TargetPath, 2));
        Assert.Equal(1, scenario.Reader.GetFieldReadCount(SourcePath, 101));
        Assert.Equal(1, scenario.Reader.GetFieldReadCount(SourcePath, 102));
    }

    [Fact]
    public void CreatePreview_WhenPatchesShareSourceIndex_ReusesIndexWithoutRetainingTargetTrees()
    {
        var scenario = CreateTwoAssetScenario();

        PatchPreviewResult preview = scenario.Builder.CreatePreview(
            TargetPath,
            [CreatePatch("A"), CreatePatch("B")],
            SourcePaths);

        Assert.Equal(2, preview.Assets.Count);
        Assert.Equal(6, scenario.Reader.TotalFieldReadCount);
        Assert.Equal(2, scenario.Reader.GetFieldReadCount(TargetPath, 1));
        Assert.Equal(2, scenario.Reader.GetFieldReadCount(TargetPath, 2));
        Assert.Equal(1, scenario.Reader.GetFieldReadCount(SourcePath, 101));
        Assert.Equal(1, scenario.Reader.GetFieldReadCount(SourcePath, 102));
    }

    [Fact]
    public void CreateWritePlan_WhenTargetDoesNotMatch_DoesNotReadSource()
    {
        var scenario = CreateTwoAssetScenario();

        IReadOnlyList<AssetReplacement> plan = scenario.Builder.CreateWritePlan(
            TargetPath,
            [CreatePatch("missing")],
            SourcePaths);

        Assert.Empty(plan);
        Assert.Equal(2, scenario.Reader.TotalFieldReadCount);
        Assert.Equal(0, scenario.Reader.GetAssetsReadCount(SourcePath));
    }

    [Fact]
    public void CreateWritePlan_WhenUnqueriedSourceValueIsDuplicated_DoesNotThrow()
    {
        var reader = new CountingAssetsFileReader(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [TargetPath] = [new AssetInfo(1, "AudioClip")],
                [SourcePath] =
                [
                    new AssetInfo(101, "AudioClip"),
                    new AssetInfo(102, "AudioClip"),
                    new AssetInfo(103, "AudioClip"),
                ],
            },
            new Dictionary<(string, long), AssetField>
            {
                [(TargetPath, 1)] = CreateFieldTree("A"),
                [(SourcePath, 101)] = CreateFieldTree("A"),
                [(SourcePath, 102)] = CreateFieldTree("B"),
                [(SourcePath, 103)] = CreateFieldTree("B"),
            });
        var builder = new ReplacementPlanner(new AssetQueryService(reader));

        AssetReplacement replacement = Assert.Single(builder.CreateWritePlan(
            TargetPath,
            [CreatePatch("A")],
            SourcePaths));

        Assert.Equal(101, replacement.SourcePathId);
    }

    [Fact]
    public void CreateWritePlan_WhenQueriedSourceValueIsDuplicated_ThrowsExistingError()
    {
        var reader = new CountingAssetsFileReader(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [TargetPath] = [new AssetInfo(1, "AudioClip")],
                [SourcePath] = [new AssetInfo(101, "AudioClip"), new AssetInfo(102, "AudioClip")],
            },
            new Dictionary<(string, long), AssetField>
            {
                [(TargetPath, 1)] = CreateFieldTree("A"),
                [(SourcePath, 101)] = CreateFieldTree("A"),
                [(SourcePath, 102)] = CreateFieldTree("A"),
            });
        var builder = new ReplacementPlanner(new AssetQueryService(reader));

        var exception = Assert.Throws<PatchPlanningException>(() => builder.CreateWritePlan(
            TargetPath,
            [CreatePatch("A")],
            SourcePaths));

        Assert.Equal(PatchDiagnosticCode.ReplacementMatchInvalid, exception.Diagnostic.Code);
        Assert.Equal(
            "Replacement source contains multiple 'AudioClip' assets with m_Name 'A'.",
            exception.Message);
    }

    [Fact]
    public void CreateWritePlan_WhenTargetValueIsDuplicated_ThrowsExistingError()
    {
        var reader = new CountingAssetsFileReader(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [TargetPath] = [new AssetInfo(1, "AudioClip"), new AssetInfo(2, "AudioClip")],
                [SourcePath] = [new AssetInfo(101, "AudioClip")],
            },
            new Dictionary<(string, long), AssetField>
            {
                [(TargetPath, 1)] = CreateFieldTree("A"),
                [(TargetPath, 2)] = CreateFieldTree("A"),
                [(SourcePath, 101)] = CreateFieldTree("A"),
            });
        var builder = new ReplacementPlanner(new AssetQueryService(reader));

        var exception = Assert.Throws<PatchPlanningException>(() => builder.CreateWritePlan(
            TargetPath,
            [CreatePatch("A")],
            SourcePaths));

        Assert.Equal(PatchDiagnosticCode.ReplacementMatchInvalid, exception.Diagnostic.Code);
        Assert.Equal(
            "Replacement target contains multiple 'AudioClip' assets with m_Name 'A'.",
            exception.Message);
    }

    [Fact]
    public void CreateWritePlan_WhenSourceDoesNotContainType_ThrowsExistingError()
    {
        var reader = new CountingAssetsFileReader(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [TargetPath] = [new AssetInfo(1, "AudioClip")],
                [SourcePath] = [],
            },
            new Dictionary<(string, long), AssetField>
            {
                [(TargetPath, 1)] = CreateFieldTree("A"),
            });
        var builder = new ReplacementPlanner(new AssetQueryService(reader));

        var exception = Assert.Throws<PatchPlanningException>(() => builder.CreateWritePlan(
            TargetPath,
            [CreatePatch("A")],
            SourcePaths));

        Assert.Equal(PatchDiagnosticCode.ReplacementMatchInvalid, exception.Diagnostic.Code);
        Assert.Equal(
            "Replacement source did not contain a 'AudioClip' asset with m_Name 'A'.",
            exception.Message);
    }

    [Fact]
    public void CreateWritePlan_WhenTargetMatchFieldIsMissing_ThrowsExistingError()
    {
        var reader = new CountingAssetsFileReader(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [TargetPath] = [new AssetInfo(1, "AudioClip")],
                [SourcePath] = [new AssetInfo(101, "AudioClip")],
            },
            new Dictionary<(string, long), AssetField>
            {
                [(TargetPath, 1)] = CreateFieldTree("A"),
                [(SourcePath, 101)] = CreateFieldTree("A"),
            });
        var builder = new ReplacementPlanner(new AssetQueryService(reader));

        var exception = Assert.Throws<PatchPlanningException>(() => builder.CreateWritePlan(
            TargetPath,
            [CreatePatch("A", "m_Key")],
            SourcePaths));

        Assert.Equal(PatchDiagnosticCode.ReplacementMatchInvalid, exception.Diagnostic.Code);
        Assert.Equal(
            "Replacement target Path ID 1 does not contain scalar match field 'm_Key'.",
            exception.Message);
        Assert.Equal(0, reader.GetAssetsReadCount(SourcePath));
    }

    [Fact]
    public void CreateWritePlan_WhenSourceMatchFieldIsNotAString_DoesNotMatchStringTarget()
    {
        var reader = new CountingAssetsFileReader(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [TargetPath] = [new AssetInfo(1, "AudioClip")],
                [SourcePath] = [new AssetInfo(101, "AudioClip")],
            },
            new Dictionary<(string, long), AssetField>
            {
                [(TargetPath, 1)] = CreateFieldTree("1"),
                [(SourcePath, 101)] = CreateFieldTree(new AssetFieldValue.Int64(1), "SInt64"),
            });
        var builder = new ReplacementPlanner(new AssetQueryService(reader));

        var exception = Assert.Throws<PatchPlanningException>(() => builder.CreateWritePlan(
            TargetPath,
            [CreatePatch("1")],
            SourcePaths));

        Assert.Equal(PatchDiagnosticCode.ReplacementMatchInvalid, exception.Diagnostic.Code);
        Assert.Equal(
            "Replacement source did not contain a 'AudioClip' asset with m_Name '1'.",
            exception.Message);
    }

    private const string TargetPath = "target.assets";
    private const string SourcePath = "source.assets";

    private static IReadOnlyDictionary<string, string> SourcePaths { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SourcePath] = SourcePath,
        };

    private static TestScenario CreateTwoAssetScenario()
    {
        var reader = new CountingAssetsFileReader(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [TargetPath] = [new AssetInfo(1, "AudioClip"), new AssetInfo(2, "AudioClip")],
                [SourcePath] = [new AssetInfo(101, "AudioClip"), new AssetInfo(102, "AudioClip")],
            },
            new Dictionary<(string, long), AssetField>
            {
                [(TargetPath, 1)] = CreateFieldTree("A"),
                [(TargetPath, 2)] = CreateFieldTree("B"),
                [(SourcePath, 101)] = CreateFieldTree("A"),
                [(SourcePath, 102)] = CreateFieldTree("B"),
            });

        return new TestScenario(reader, new ReplacementPlanner(new AssetQueryService(reader)));
    }

    private static ManifestPatch CreatePatch(string name, string matchFieldPath = "m_Name")
    {
        return new ManifestPatch(
            TargetPath,
            "AudioClip",
            new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal)
            {
                ["m_Name"] = JsonElementFactory.String(name),
            },
            null,
            null,
            new ManifestReplaceFrom(SourcePath, matchFieldPath));
    }

    private static AssetField CreateFieldTree(string name)
    {
        return CreateFieldTree(new AssetFieldValue.String(name), "string");
    }

    private static AssetField CreateFieldTree(AssetFieldValue value, string typeName)
    {
        return new AssetField(
            "AudioClip",
            "AudioClip",
            null,
            [new AssetField("m_Name", typeName, value, [])]);
    }

    private sealed record TestScenario(
        CountingAssetsFileReader Reader,
        ReplacementPlanner Builder);

    private sealed class CountingAssetsFileReader : IAssetsFileReader
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<AssetInfo>> _assetsByPath;
        private readonly IReadOnlyDictionary<(string, long), AssetField> _fieldsByAsset;
        private readonly Dictionary<string, int> _assetsReadCounts = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<(string, long), int> FieldReadCounts => _fieldReadCounts;
        public int TotalFieldReadCount => _fieldReadCounts.Values.Sum();

        private readonly Dictionary<(string, long), int> _fieldReadCounts = new();

        public CountingAssetsFileReader(
            IReadOnlyDictionary<string, IReadOnlyList<AssetInfo>> assetsByPath,
            IReadOnlyDictionary<(string, long), AssetField> fieldsByAsset)
        {
            _assetsByPath = assetsByPath;
            _fieldsByAsset = fieldsByAsset;
        }

        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            _assetsReadCounts[assetsFilePath] = GetAssetsReadCount(assetsFilePath) + 1;
            return _assetsByPath[assetsFilePath];
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            var key = (assetsFilePath, pathId);
            _fieldReadCounts[key] = GetFieldReadCount(assetsFilePath, pathId) + 1;
            return _fieldsByAsset[key];
        }

        public void Dispose() { }

        public int GetAssetsReadCount(string assetsFilePath)
        {
            return _assetsReadCounts.GetValueOrDefault(assetsFilePath);
        }

        public int GetFieldReadCount(string assetsFilePath, long pathId)
        {
            return _fieldReadCounts.GetValueOrDefault((assetsFilePath, pathId));
        }
    }
}
