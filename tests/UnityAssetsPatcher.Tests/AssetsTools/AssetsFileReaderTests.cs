using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsFileReaderTests
{
    /// <summary>
    /// Verifies that the reader can load a real Unity assets file through AssetsTools.NET.
    /// </summary>
    [Fact]
    public void ReadAssetsInfoAndFieldInfo_WhenRealAssetsFileExists_ReturnsAssetData()
    {
        var reader = new AssetsFileReader(GetRealTpkFilePath());

        var assets = reader.ReadAssetsInfo(GetRealAssetsFilePath());
        AssetsInfo asset = Assert.Single(assets.Take(1));
        AssetsFieldInfo fieldTree = reader.ReadAssetsFieldInfo(GetRealAssetsFilePath(), asset.PathId);

        Assert.NotEmpty(assets);
        Assert.NotEqual(0, asset.PathId);
        Assert.False(string.IsNullOrWhiteSpace(asset.TypeName));
        Assert.False(string.IsNullOrWhiteSpace(fieldTree.Name));
        Assert.False(string.IsNullOrWhiteSpace(fieldTree.TypeName));
    }

    /// <summary>
    /// Verifies that one reader instance can reuse a real Unity assets file session.
    /// </summary>
    [Fact]
    public void ReadAssetsInfoAndFieldInfo_WhenReaderInstanceIsReused_ReturnsAssetData()
    {
        using var reader = new AssetsFileReader(GetRealTpkFilePath());

        var assets = reader.ReadAssetsInfo(GetRealAssetsFilePath());
        AssetsInfo asset = Assert.Single(assets.Take(1));
        AssetsFieldInfo fieldTree = reader.ReadAssetsFieldInfo(GetRealAssetsFilePath(), asset.PathId);

        Assert.NotEmpty(assets);
        Assert.NotEqual(0, asset.PathId);
        Assert.False(string.IsNullOrWhiteSpace(fieldTree.Name));
    }

    /// <summary>
    /// Verifies that the reader returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void ReadAssetSummaries_WhenAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        var service = new AssetsFileReader("AssetsRipper.tpk");

        var exception = Assert.Throws<FileNotFoundException>(() => service.ReadAssetsInfo(missingAssetsFile));

        Assert.Equal($"Assets file not found: {missingAssetsFile}", exception.Message);
    }

    /// <summary>
    /// Verifies that the reader returns a clear error with the file path when the TPK type database is missing.
    /// </summary>
    [Fact]
    public void ReadAssetSummaries_WhenTpkFileDoesNotExist_ThrowsClearError()
    {
        string existingAssetsFile = Path.GetTempFileName();
        string missingTpkFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tpk");
        var service = new AssetsFileReader(missingTpkFile);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() => service.ReadAssetsInfo(existingAssetsFile));

            Assert.Equal($"TPK file not found: {missingTpkFile}", exception.Message);
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
            if (File.Exists(Path.Combine(directory, "UnityAssetsPatcher.sln")))
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

    private static string GetRealTpkFilePath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Assets",
            "AssetsRipper.tpk");
    }
}
