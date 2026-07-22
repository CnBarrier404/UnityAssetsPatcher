using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Tests;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Installation;

public sealed class InstallPlanBuilderTests
{
    [Fact]
    public void Analyze_WhenModesPlanSamePackage_PreservesTargetsPlansAndPayloadOrder()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "files": [
                { "source": "resources/first.resource" },
                { "source": "resources/second.resource" }
              ],
              "patches": [
                {
                  "target": "sharedassets0.assets",
                  "type": "Camera",
                  "match": { "field of view": 90.0 },
                  "set": [
                    {
                      "field": "m_CullingMask.m_Bits",
                      "from": 3211820983,
                      "to": 931037111
                    }
                  ]
                }
              ]
            }
            """);
        var assetsFileService = new StubAssetsFileService(
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField>
            {
                [4] = new("Camera", "Camera", null,
                [
                    new AssetField("field of view", "float", new AssetFieldValue.Float(90f), []),
                    new AssetField("m_CullingMask", "BitField", null,
                    [
                        new AssetField("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
                    ]),
                ]),
            });
        InstallPlanBuilder builder = CreateBuilder();

        try
        {
            using ModPackage package = ModPackage.Open(
                zipPath,
                [],
                new ModManifestReader(),
                TestDependencies.DirectoryOperations,
                new StepTimer());

            InstallAnalysis detailed = builder.Analyze(
                package,
                gameDirectory,
                InstallAnalysisMode.PreviewDetailed,
                assetsFileService,
                new StepTimer());
            InstallAnalysis summary = builder.Analyze(
                package,
                gameDirectory,
                InstallAnalysisMode.PreviewSummary,
                assetsFileService,
                new StepTimer());
            InstallAnalysis apply = builder.Analyze(
                package,
                gameDirectory,
                InstallAnalysisMode.Apply,
                assetsFileService,
                new StepTimer());

            Assert.Same(package.Manifest, detailed.Manifest);
            Assert.Equal(detailed.GameDirectory, summary.GameDirectory);
            Assert.Equal(detailed.GameDirectory, apply.GameDirectory);
            Assert.Equal(
                detailed.Targets.Select(target => (target.Target, target.AssetsFilePath)),
                summary.Targets.Select(target => (target.Target, target.AssetsFilePath)));
            Assert.Equal(
                detailed.Targets.Select(target => (target.Target, target.AssetsFilePath)),
                apply.Targets.Select(target => (target.Target, target.AssetsFilePath)));
            Assert.Equal(detailed.PayloadFiles, summary.PayloadFiles);
            Assert.Equal(detailed.PayloadFiles, apply.PayloadFiles);
            Assert.Equal(
                ["resources/first.resource", "resources/second.resource"],
                apply.PayloadFiles.Select(file => file.Source));

            PatchPlanningResult detailedPlanning = Assert.Single(detailed.Targets).PlanningResult;
            PatchPlanningResult summaryPlanning = Assert.Single(summary.Targets).PlanningResult;
            PatchPlanningResult applyPlanning = Assert.Single(apply.Targets).PlanningResult;
            Assert.True(detailedPlanning.CanApply);
            Assert.Equal(detailedPlanning.CanApply, summaryPlanning.CanApply);
            Assert.Equal(detailedPlanning.Diagnostic, summaryPlanning.Diagnostic);
            Assert.Single(detailedPlanning.Preview.Assets);
            Assert.Empty(summaryPlanning.Preview.Assets);
            Assert.Empty(applyPlanning.Preview.Assets);
            Assert.Equal(
                Assert.Single(detailed.Targets).MatchCount,
                Assert.Single(summary.Targets).MatchCount);
            Assert.Equal(
                Assert.Single(detailed.Targets).MatchCount,
                Assert.Single(apply.Targets).MatchCount);
            Assert.Equal(GetOperationCount(detailedPlanning), GetOperationCount(summaryPlanning));
            Assert.Equal(GetOperationCount(detailedPlanning), GetOperationCount(applyPlanning));
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void Analyze_WhenTargetDoesNotMatch_SummaryPreservesDiagnosticAndCanApply()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string targetPath = Path.Combine(gameDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(gameDirectory);
        File.WriteAllText(targetPath, "original");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "target": "sharedassets0.assets",
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": { "field of view": { "from": 90.0, "to": 75.0 } }
            }
            """);
        var assetsFileService = new StubAssetsFileService([]);
        InstallPlanBuilder builder = CreateBuilder();

        try
        {
            using ModPackage package = ModPackage.Open(
                zipPath,
                [],
                new ModManifestReader(),
                TestDependencies.DirectoryOperations,
                new StepTimer());

            InstallAnalysis detailed = builder.Analyze(
                package,
                gameDirectory,
                InstallAnalysisMode.PreviewDetailed,
                assetsFileService,
                new StepTimer());
            InstallAnalysis summary = builder.Analyze(
                package,
                gameDirectory,
                InstallAnalysisMode.PreviewSummary,
                assetsFileService,
                new StepTimer());

            PatchPlanningResult detailedPlanning = Assert.Single(detailed.Targets).PlanningResult;
            PatchPlanningResult summaryPlanning = Assert.Single(summary.Targets).PlanningResult;
            Assert.False(detailedPlanning.CanApply);
            Assert.Equal(detailedPlanning.CanApply, summaryPlanning.CanApply);
            Assert.Equal(detailedPlanning.Diagnostic, summaryPlanning.Diagnostic);
            Assert.Equal(PatchDiagnosticCode.NoMatchingAssets, summaryPlanning.Diagnostic!.Code);
            Assert.Empty(summaryPlanning.Preview.Assets);
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void PlanPayloadFiles_WhenTargetsResolveToDifferentDirectories_ThrowsClearError()
    {
        var manifest = CreateManifest([new ManifestFile("resources/modassets.resource")]);
        var targets = new TargetAssetSet(
        [
            new TargetAsset("sharedassets0.assets", FullPath("Game_Data", "sharedassets0.assets"), []),
            new TargetAsset("sharedassets1.assets", FullPath("Other_Data", "sharedassets1.assets"), []),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            InstallPlanBuilder.PlanPayloadFiles(manifest, targets));

        Assert.Contains("Payload files require all patch targets to resolve to the same directory", exception.Message);
    }

    private static ModManifest CreateManifest(IReadOnlyList<ManifestFile> files)
    {
        return new ModManifest(1, "Test Mod", "Tester", "1.0.0", null, null, files, [], []);
    }

    private static InstallPlanBuilder CreateBuilder()
    {
        return new InstallPlanBuilder(
            new TargetAssetResolver(),
            new GameDirectoryResolver(),
            [new SetFieldPatchOperationHandler(), new AddFieldPatchOperationHandler()]);
    }

    private static int GetOperationCount(PatchPlanningResult planningResult)
    {
        FieldPatchPlan plan = Assert.IsType<FieldPatchPlan>(planningResult.Plan);

        return plan.Assets.Sum(asset => asset.Operations.Count);
    }

    private static string FullPath(params string[] parts)
    {
        return Path.GetFullPath(Path.Combine([Path.GetTempPath(), .. parts]));
    }
}
