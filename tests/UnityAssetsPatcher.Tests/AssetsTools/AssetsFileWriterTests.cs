using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Tests;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsFileWriterTests
{
    /// <summary>
    /// Verifies that patch writing returns a clear error with the file path when the target assets file is missing.
    /// </summary>
    [Fact]
    public void WritePatch_WhenAssetsFileDoesNotExist_ThrowsClearError()
    {
        string missingAssetsFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        using var writer = new AssetsFileWriter(
            "AssetsRipper.tpk",
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations);

        var exception = Assert.Throws<FileNotFoundException>(() =>
            writer.WriteFieldPatches(missingAssetsFile, outputPath, []));

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
        using var writer = new AssetsFileWriter(
            missingTpkFile,
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() =>
                writer.WriteFieldPatches(existingAssetsFile, outputPath, []));

            Assert.Equal($"TPK file not found: {missingTpkFile}", exception.Message);
        }
        finally
        {
            File.Delete(existingAssetsFile);
            File.Delete(outputPath);
        }
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
        var writer = new AssetsFileWriter(
            missingTpkFile,
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() =>
                writer.WriteReplacements(existingAssetsFile, outputPath,
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
