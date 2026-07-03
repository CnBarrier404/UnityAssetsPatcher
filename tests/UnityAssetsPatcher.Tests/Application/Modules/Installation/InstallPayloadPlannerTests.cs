using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules.Installation;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Modules.Installation;

public sealed class InstallPayloadPlannerTests
{
    [Fact]
    public void Plan_WhenManifestHasNoPayloadFiles_ReturnsEmptyPlan()
    {
        var planner = new InstallPayloadPlanner();
        var manifest = CreateManifest([]);
        var targets = new TargetAssetSet(
        [
            new TargetAsset("sharedassets0.assets", FullPath("Game_Data", "sharedassets0.assets"), []),
        ]);

        IReadOnlyList<InstallPayloadFilePlan> result = planner.Plan(manifest, targets);

        Assert.Empty(result);
    }

    [Fact]
    public void Plan_WhenSingleTargetDirectoryExists_UsesTargetDirectoryForPayloadDestination()
    {
        var planner = new InstallPayloadPlanner();
        var manifest = CreateManifest([new ManifestFile("resources/modassets.resource")]);
        string targetPath = FullPath("Game_Data", "sharedassets0.assets");
        var targets = new TargetAssetSet(
        [
            new TargetAsset("sharedassets0.assets", targetPath, []),
        ]);

        InstallPayloadFilePlan result = Assert.Single(planner.Plan(manifest, targets));

        Assert.Equal("resources/modassets.resource", result.Source);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(targetPath)!, "modassets.resource"), result.DestinationPath);
    }

    [Fact]
    public void Plan_WhenPayloadSourceDoesNotNameFile_ThrowsClearError()
    {
        var planner = new InstallPayloadPlanner();
        var manifest = CreateManifest([new ManifestFile("resources/")]);
        var targets = new TargetAssetSet(
        [
            new TargetAsset("sharedassets0.assets", FullPath("Game_Data", "sharedassets0.assets"), []),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => planner.Plan(manifest, targets));

        Assert.Contains("Payload source must name a file", exception.Message);
        Assert.Contains("resources/", exception.Message);
    }

    [Fact]
    public void Plan_WhenPayloadExistsWithoutPatchTarget_ThrowsClearError()
    {
        var planner = new InstallPayloadPlanner();
        var manifest = CreateManifest([new ManifestFile("resources/modassets.resource")]);
        var targets = new TargetAssetSet([]);

        var exception = Assert.Throws<InvalidOperationException>(() => planner.Plan(manifest, targets));

        Assert.Contains("Payload files require at least one patch target", exception.Message);
    }

    [Fact]
    public void Plan_WhenTargetsResolveToDifferentDirectories_ThrowsClearError()
    {
        var planner = new InstallPayloadPlanner();
        var manifest = CreateManifest([new ManifestFile("resources/modassets.resource")]);
        var targets = new TargetAssetSet(
        [
            new TargetAsset("sharedassets0.assets", FullPath("Game_Data", "sharedassets0.assets"), []),
            new TargetAsset("sharedassets1.assets", FullPath("Other_Data", "sharedassets1.assets"), []),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => planner.Plan(manifest, targets));

        Assert.Contains("Payload files require all patch targets to resolve to the same directory", exception.Message);
    }

    private static ModManifest CreateManifest(IReadOnlyList<ManifestFile> files)
    {
        return new ModManifest(new ModInfo("Test Mod", "Tester", "1.0.0", null, null), files, [], []);
    }

    private static string FullPath(params string[] parts)
    {
        return Path.GetFullPath(Path.Combine([Path.GetTempPath(), .. parts]));
    }
}
