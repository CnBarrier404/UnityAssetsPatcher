using System.Text.Json;
using System.Xml.Linq;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class ArchitectureComponentTests
{
    [Fact]
    public void ProjectReferences_FollowAllowedAssemblyDependencyDirection()
    {
        string root = FindRepositoryRoot();

        AssertProjectReferences(
            Path.Combine(root, "src", "UnityAssetsPatcher.Core", "UnityAssetsPatcher.Core.csproj"),
            expectedProjectReferences: [],
            expectedPackageReferences: []);
        AssertProjectReferences(
            Path.Combine(root, "src", "UnityAssetsPatcher.Application", "UnityAssetsPatcher.Application.csproj"),
            expectedProjectReferences: ["UnityAssetsPatcher.Core.csproj"],
            expectedPackageReferences: []);
        AssertProjectReferences(
            Path.Combine(root, "src", "UnityAssetsPatcher.AssetsTools", "UnityAssetsPatcher.AssetsTools.csproj"),
            expectedProjectReferences: ["UnityAssetsPatcher.Core.csproj"],
            expectedPackageReferences: ["AssetsTools.NET"]);
        AssertProjectReferences(
            Path.Combine(root, "src", "UnityAssetsPatcher", "UnityAssetsPatcher.csproj"),
            expectedProjectReferences:
            [
                "UnityAssetsPatcher.Application.csproj",
                "UnityAssetsPatcher.AssetsTools.csproj",
                "UnityAssetsPatcher.Core.csproj",
            ],
            expectedPackageReferences: ["Spectre.Console"]);
    }

    [Fact]
    public void PatchTargetSelector_WhenAssetsPathHasDifferentCasing_SelectsTargetsByFileName()
    {
        var manifest = new ModManifest(
            "Test Mod",
            "UnityAssetsPatcher.Tests",
            "1.0.0",
            null,
            [],
            [
                new ManifestPatch("sharedassets0.assets", "Camera", [], null, null),
                new ManifestPatch("resources.assets", "Camera", [], null, null),
            ]);

        var targets =
            PatchTargetSelector.ForAssetsFile(manifest, @"C:\Game\SHAREDASSETS0.ASSETS");

        ManifestPatch target = Assert.Single(targets);
        Assert.Equal("sharedassets0.assets", target.AssetsFileName);
    }

    [Fact]
    public void InstallTargetResolver_WhenTargetMatchesMultipleFiles_Throws()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string firstDirectory = Path.Combine(gameDirectory, "Game_Data");
        string secondDirectory = Path.Combine(gameDirectory, "Backup_Data");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        File.WriteAllText(Path.Combine(firstDirectory, "sharedassets0.assets"), "original");
        File.WriteAllText(Path.Combine(secondDirectory, "sharedassets0.assets"), "original");

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                InstallTargetResolver.Resolve(gameDirectory, ["sharedassets0.assets"]));

            Assert.Contains("matched multiple files", exception.Message);
        }
        finally
        {
            Directory.Delete(gameDirectory, recursive: true);
        }
    }

    [Fact]
    public void PatchOutputWriter_WhenExplicitOutputPointsToInput_ThrowsBeforeWriting()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        File.WriteAllText(inputPath, "original");
        var writer = new PatchOutputWriter(new RecordingAssetsFileService());

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                writer.WriteFieldPatch(
                    inputPath,
                    inputPath,
                    Path.GetTempPath(),
                    [
                        new PatchWriteAsset(1,
                            [new PatchWriteOperation("m_Name", "old", JsonSerializer.SerializeToElement("new"))])
                    ]));

            Assert.Equal("--output cannot point to the input assets file.", exception.Message);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    private sealed class RecordingAssetsFileService : IAssetsPatchWriter
    {
        public void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan)
        {
            throw new InvalidOperationException("Writer should not be called.");
        }

        public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
        {
            throw new InvalidOperationException("Writer should not be called.");
        }
    }

    private static void AssertProjectReferences(
        string projectPath,
        IReadOnlyList<string> expectedProjectReferences,
        IReadOnlyList<string> expectedPackageReferences)
    {
        XDocument document = XDocument.Load(projectPath);
        string?[] projectReferences = document.Descendants("ProjectReference")
            .Select(element => Path.GetFileName(element.Attribute("Include")?.Value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string?[] packageReferences = document.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedProjectReferences.OrderBy(value => value, StringComparer.Ordinal), projectReferences);
        Assert.Equal(expectedPackageReferences.OrderBy(value => value, StringComparer.Ordinal), packageReferences);
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
}
