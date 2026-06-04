using UnityAssetsPatcher.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class AssetsToolsReaderTests
{
    /// <summary>
    /// Verifies that the reader returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void ReadAssetSummaries_WhenAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        var reader = new AssetsToolsReader("AssetsRipper.tpk");

        var exception = Assert.Throws<FileNotFoundException>(() => reader.ReadAssetsInfo(missingAssetsFile));

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
        var reader = new AssetsToolsReader(missingTpkFile);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() => reader.ReadAssetsInfo(existingAssetsFile));

            Assert.Equal($"TPK file not found: {missingTpkFile}", exception.Message);
        }
        finally
        {
            File.Delete(existingAssetsFile);
        }
    }

    /// <summary>
    /// Verifies that patch writing returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void WritePatch_WhenAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        var reader = new AssetsToolsReader("AssetsRipper.tpk");

        var exception = Assert.Throws<FileNotFoundException>(() =>
            reader.WritePatch(missingAssetsFile, outputPath, []));

        Assert.Equal($"Assets file not found: {missingAssetsFile}", exception.Message);
    }

    /// <summary>
    /// Verifies that patch writing returns a clear error with the file path when the TPK type database is missing.
    /// </summary>
    [Fact]
    public void WritePatch_WhenTpkFileDoesNotExist_ThrowsClearError()
    {
        string existingAssetsFile = Path.GetTempFileName();
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string missingTpkFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tpk");
        var reader = new AssetsToolsReader(missingTpkFile);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() =>
                reader.WritePatch(existingAssetsFile, outputPath, []));

            Assert.Equal($"TPK file not found: {missingTpkFile}", exception.Message);
        }
        finally
        {
            File.Delete(existingAssetsFile);
            File.Delete(outputPath);
        }
    }
}
