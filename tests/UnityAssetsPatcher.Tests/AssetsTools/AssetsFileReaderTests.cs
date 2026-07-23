using System.Collections;
using System.Reflection;
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
        using var reader = new AssetsFileReader(new AssetsToolsContext(GetRealTpkFilePath()));

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
    }

    /// <summary>
    /// Verifies that the reader returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void ReadAssets_WhenAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        using var service = new AssetsFileReader(new AssetsToolsContext("AssetsRipper.tpk"));

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
        using var service = new AssetsFileReader(new AssetsToolsContext(missingTpkFile));

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() => service.ReadAssets(existingAssetsFile));

            Assert.Equal($"TPK file not found: {missingTpkFile}", exception.Message);
        }
        finally
        {
            File.Delete(existingAssetsFile);
        }
    }

    [Fact]
    public void Dispose_WhenSessionDisposeFails_ClearsSessionCacheAndRethrows()
    {
        var reader = new AssetsFileReader(new AssetsToolsContext(GetRealTpkFilePath()));
        IDictionary sessions = GetPrivateDictionary(reader, "_sessions");
        sessions.Add(GetRealAssetsFilePath(), null);

        Assert.Throws<NullReferenceException>(reader.Dispose);

        Assert.Empty(sessions);
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

    private static string GetRealTpkFilePath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Assets",
            "resources.tpk");
    }

    private static IDictionary GetPrivateDictionary(AssetsFileReader reader, string fieldName)
    {
        FieldInfo field = typeof(AssetsFileReader).GetField(
                              fieldName,
                              BindingFlags.Instance | BindingFlags.NonPublic) ??
                          throw new InvalidOperationException($"Field not found: {fieldName}");

        return (IDictionary)(field.GetValue(reader) ??
                             throw new InvalidOperationException($"Field value was null: {fieldName}"));
    }
}
