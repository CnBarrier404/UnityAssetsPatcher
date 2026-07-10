using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Installation;

public sealed class InstallPlannerTests
{
    [Fact]
    public void PlanPayloadFiles_WhenPayloadSourceDoesNotNameFile_ThrowsClearError()
    {
        var manifest = CreateManifest([new ManifestFile("resources/")]);
        var targets = new TargetAssetSet(
        [
            new TargetAsset("sharedassets0.assets", FullPath("Game_Data", "sharedassets0.assets"), []),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            InstallPlanner.PlanPayloadFiles(manifest, targets));

        Assert.Contains("Payload source must name a file", exception.Message);
        Assert.Contains("resources/", exception.Message);
    }

    [Fact]
    public void PlanPayloadFiles_WhenPayloadExistsWithoutPatchTarget_ThrowsClearError()
    {
        var manifest = CreateManifest([new ManifestFile("resources/modassets.resource")]);
        var targets = new TargetAssetSet([]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            InstallPlanner.PlanPayloadFiles(manifest, targets));

        Assert.Contains("Payload files require at least one patch target", exception.Message);
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
            InstallPlanner.PlanPayloadFiles(manifest, targets));

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
