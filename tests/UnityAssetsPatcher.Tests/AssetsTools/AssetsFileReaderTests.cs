using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsFileReaderTests
{
    /// <summary>
    /// Verifies that one reader instance can reuse a real Unity assets file session.
    /// </summary>
    [Fact]
    public void ReadAssetsAndField_WhenReaderInstanceIsReused_ReturnsAssetData()
    {
        int classPackageOpenCount = 0;
        using var reader = new AssetsFileReader(
            new ClassPackageCache(() =>
            {
                classPackageOpenCount++;

                return OpenRealTpkStream();
            }));

        var assets = reader.ReadAssets(GetRealAssetsFilePath());
        AssetInfo asset = Assert.Single(assets.Take(1));
        AssetField fieldTree = reader.ReadField(GetRealAssetsFilePath(), asset.PathId);
        var repeatedAssets = reader.ReadAssets(GetRealAssetsFilePath());
        AssetField repeatedFieldTree = reader.ReadField(GetRealAssetsFilePath(), asset.PathId);

        Assert.NotEmpty(assets);
        Assert.NotEqual(0, asset.PathId);
        Assert.False(string.IsNullOrWhiteSpace(asset.TypeName));
        Assert.False(string.IsNullOrWhiteSpace(fieldTree.Name));
        Assert.False(string.IsNullOrWhiteSpace(fieldTree.TypeName));
        Assert.Equal(assets, repeatedAssets);
        Assert.Equal(fieldTree.Name, repeatedFieldTree.Name);
        Assert.Equal(fieldTree.TypeName, repeatedFieldTree.TypeName);
        Assert.Equal(fieldTree.Children.Count, repeatedFieldTree.Children.Count);
        Assert.Equal(1, classPackageOpenCount);
    }

    /// <summary>
    /// Verifies that the reader returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void ReadAssets_WhenAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        using var service = new AssetsFileReader(new ClassPackageCache(OpenRealTpkStream));

        var exception = Assert.Throws<FileNotFoundException>(() => service.ReadAssets(missingAssetsFile));

        Assert.Equal($"Assets file not found: {missingAssetsFile}", exception.Message);
    }

    /// <summary>
    /// Verifies that the reader returns a clear error with the file path when the TPK type database is missing.
    /// </summary>
    [Fact]
    public void ReadAssets_WhenTpkFileDoesNotExist_ThrowsClearError()
    {
        string existingAssetsFile = Path.GetTempFileName();
        string missingTpkFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tpk");
        using var service = new AssetsFileReader(
            new ClassPackageCache(() => File.OpenRead(missingTpkFile)));

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() => service.ReadAssets(existingAssetsFile));

            Assert.Equal(missingTpkFile, exception.FileName);
        }
        finally
        {
            File.Delete(existingAssetsFile);
        }
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
