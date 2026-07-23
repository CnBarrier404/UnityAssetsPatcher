using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsToolsAccessScopeTests
{
    [Fact]
    public void Dispose_WhenCalledMultipleTimes_RemainsIdempotentAndRejectsAccess()
    {
        var scope = CreateScope();
        IAssetsFileReader reader = scope.Reader;

        scope.Dispose();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => reader.ReadAssets(GetRealAssetsFilePath()));
    }

    [Fact]
    public void WriteFieldPatches_WhenFileWasReadFirst_AutomaticallyClosesReadSession()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.assets");
        var scope = CreateScope();
        IReadOnlyList<UnityAssetsPatcher.Domain.Assets.AssetInfo> expected =
            scope.Reader.ReadAssets(GetRealAssetsFilePath());

        try
        {
            scope.Writer.WriteFieldPatches(GetRealAssetsFilePath(), outputPath, []);
            IReadOnlyList<UnityAssetsPatcher.Domain.Assets.AssetInfo> actual = scope.Reader.ReadAssets(outputPath);

            Assert.Equal(expected, actual);
            scope.Dispose();
        }
        finally
        {
            scope.Dispose();
            File.Delete(outputPath);
        }
    }

    private static AssetsToolsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(
            OpenRealTpkStream,
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations);
    }

    private static string FindRepositoryRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "UnityAssetsPatcher.slnx")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string GetRealAssetsFilePath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "UnityAssetsPatcher.Tests",
            "RealTestAssets",
            "sharedassets0.assets");
    }

    private static Stream OpenRealTpkStream()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Assets",
            "resources.tpk");

        return File.OpenRead(path);
    }
}
