using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class AssetsFileServiceReadTests
{
    /// <summary>
    /// Verifies that the reader returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void ReadAssetSummaries_WhenAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        var service = new AssetsFileService("AssetsRipper.tpk");

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
        var service = new AssetsFileService(missingTpkFile);

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
}

public sealed class AssetsFileServiceWriteTests
{
    /// <summary>
    /// Verifies that patch writing returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void WritePatch_WhenAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        var service = new AssetsFileService("AssetsRipper.tpk");

        var exception = Assert.Throws<FileNotFoundException>(() =>
            service.WritePatch(missingAssetsFile, outputPath, []));

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
        var service = new AssetsFileService(missingTpkFile);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() =>
                service.WritePatch(existingAssetsFile, outputPath, []));

            Assert.Equal($"TPK file not found: {missingTpkFile}", exception.Message);
        }
        finally
        {
            File.Delete(existingAssetsFile);
            File.Delete(outputPath);
        }
    }

    /// <summary>
    /// Verifies that replacement writing returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void WriteReplacements_WhenTargetAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        var service = new AssetsFileService("AssetsRipper.tpk");

        var exception = Assert.Throws<FileNotFoundException>(() =>
            service.WriteReplacements(missingAssetsFile, outputPath, []));

        Assert.Equal($"Assets file not found: {missingAssetsFile}", exception.Message);
    }

    /// <summary>
    /// Verifies that replacement writing returns a clear error with the file path when the source assets file is missing.
    /// </summary>
    [Fact]
    public void WriteReplacements_WhenSourceAssetsFileDoesNotExist_ThrowsClearError()
    {
        string existingAssetsFile = Path.GetTempFileName();
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string missingSourceAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string missingTpkFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tpk");
        var service = new AssetsFileService(missingTpkFile);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() =>
                service.WriteReplacements(existingAssetsFile, outputPath,
                [
                    new AssetReplacement(missingSourceAssetsFile, 1, 2),
                ]));

            Assert.Equal($"Assets file not found: {missingSourceAssetsFile}", exception.Message);
        }
        finally
        {
            File.Delete(existingAssetsFile);
            File.Delete(outputPath);
        }
    }
}
