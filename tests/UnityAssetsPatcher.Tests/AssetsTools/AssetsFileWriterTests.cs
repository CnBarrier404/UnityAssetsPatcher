using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Json;
using UnityAssetsPatcher.Tests;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsFileWriterTests
{
    [Fact]
    public void WriteFieldPatches_WhenScalarAndArrayAreChanged_WritesReopenableOutputAndPreservesInput()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.assets");
        var writer = CreateRealWriter();

        try
        {
            writer.WriteFieldPatches(GetRealAssetsFilePath(), outputPath,
            [
                new AssetFieldPatch(4, [new FieldPatchOperation("m_Name", JsonElementFactory.String("Changed"))]),
                new AssetFieldPatch(1, [new FieldPatchOperation("m_Assets.Array", JsonElementFactory.Array([]))]),
            ]);
            using var reader = new AssetsFileReader(new ClassPackageCache(OpenRealTpkStream));

            Assert.Equal("Changed", Find(reader.ReadField(outputPath, 4), "m_Name").Value?.ToInvariantString());
            Assert.Empty(Find(reader.ReadField(outputPath, 1), "m_Assets.Array").Children);
            Assert.Equal("Text",
                Find(reader.ReadField(GetRealAssetsFilePath(), 4), "m_Name").Value?.ToInvariantString());
            Assert.Equal(3, Find(reader.ReadField(GetRealAssetsFilePath(), 1), "m_Assets.Array").Children.Count);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void ReadField_WhenDifferentDirectoriesContainSameNamedFiles_KeepsSessionsIsolatedByFullPath()
    {
        string root = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher-{Guid.NewGuid():N}");
        string firstPath = Path.Combine(root, "first", "sharedassets0.assets");
        string secondPath = Path.Combine(root, "second", "sharedassets0.assets");
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);
        File.Copy(GetRealAssetsFilePath(), firstPath);
        var writer = CreateRealWriter();

        try
        {
            writer.WriteFieldPatches(GetRealAssetsFilePath(), secondPath,
            [
                new AssetFieldPatch(4, [new FieldPatchOperation("m_Name", JsonElementFactory.String("Second"))]),
            ]);
            using var reader = new AssetsFileReader(new ClassPackageCache(OpenRealTpkStream));

            Assert.Equal("Text", Find(reader.ReadField(firstPath, 4), "m_Name").Value?.ToInvariantString());
            Assert.Equal("Second", Find(reader.ReadField(secondPath, 4), "m_Name").Value?.ToInvariantString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WriteReplacements_WhenSourceContainsModifiedAsset_ReplacesTargetAndWritesReopenableOutput()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.assets");
        var writer = CreateRealWriter();

        try
        {
            writer.WriteFieldPatches(GetRealAssetsFilePath(), sourcePath,
            [
                new AssetFieldPatch(4, [new FieldPatchOperation("m_Name", JsonElementFactory.String("Replacement"))]),
            ]);
            writer.WriteReplacements(GetRealAssetsFilePath(), outputPath,
            [
                new AssetReplacement(sourcePath, 4, 4),
            ]);
            using var reader = new AssetsFileReader(new ClassPackageCache(OpenRealTpkStream));

            Assert.Equal("Replacement", Find(reader.ReadField(outputPath, 4), "m_Name").Value?.ToInvariantString());
            Assert.Equal("Text",
                Find(reader.ReadField(GetRealAssetsFilePath(), 4), "m_Name").Value?.ToInvariantString());
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void WriteReplacements_WhenTargetAndSourceSessionsAreOpened_LoadsClassPackageOnce()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.assets");
        int classPackageOpenCount = 0;
        var writer = new AssetsFileWriter(
            new ClassPackageCache(() =>
            {
                classPackageOpenCount++;

                return OpenRealTpkStream();
            }),
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations);
        File.Copy(GetRealAssetsFilePath(), sourcePath);

        try
        {
            writer.WriteReplacements(GetRealAssetsFilePath(), outputPath,
            [
                new AssetReplacement(sourcePath, 4, 4),
            ]);

            Assert.Equal(1, classPackageOpenCount);
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(outputPath);
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
        var writer = new AssetsFileWriter(
            new ClassPackageCache(() => throw new InvalidOperationException("TPK should not be opened.")),
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
        var writer = new AssetsFileWriter(
            new ClassPackageCache(() => File.OpenRead(missingTpkFile)),
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() =>
                writer.WriteFieldPatches(existingAssetsFile, outputPath, []));

            Assert.Equal(missingTpkFile, exception.FileName);
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
            new ClassPackageCache(() => File.OpenRead(missingTpkFile)),
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

    private static AssetsFileWriter CreateRealWriter()
    {
        return new AssetsFileWriter(
            new ClassPackageCache(OpenRealTpkStream),
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations);
    }

    private static AssetField Find(AssetField root, string path)
    {
        return AssetFieldPath.Find(
                   root,
                   path,
                   field => field.Name,
                   field => field.Children,
                   field => field.Value?.ToInvariantString(),
                   (field, name) => field.Children.Where(child => child.Name == name)) ??
               throw new InvalidOperationException($"Field not found: {path}");
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
