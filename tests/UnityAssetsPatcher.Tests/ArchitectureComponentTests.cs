using System.Xml.Linq;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;
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
    public void InstallTargetResolver_WhenMultipleTargetsAreProvided_EnumeratesFilesOnce()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string dataDirectory = Path.Combine(gameDirectory, "Game_Data");
        Directory.CreateDirectory(dataDirectory);
        string resourcesPath = Path.Combine(dataDirectory, "resources.assets");
        string sharedAssetsPath = Path.Combine(dataDirectory, "sharedassets0.assets");
        var files = new CountingEnumerable<string>(
        [
            resourcesPath,
            sharedAssetsPath,
            Path.Combine(dataDirectory, "globalgamemanagers"),
        ]);

        try
        {
            var result = InstallTargetResolver.Resolve(
                gameDirectory,
                ["resources.assets", "sharedassets0.assets"],
                files);

            Assert.Equal(1, files.EnumerationCount);
            Assert.Equal(Path.GetFullPath(resourcesPath), result["resources.assets"]);
            Assert.Equal(Path.GetFullPath(sharedAssetsPath), result["sharedassets0.assets"]);
        }
        finally
        {
            Directory.Delete(gameDirectory, recursive: true);
        }
    }

    [Fact]
    public void GameDirectoryResolver_WhenSteamAppManifestNameMatchesGame_ReturnsInstallDirectory()
    {
        string steamDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Steam");
        string libraryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "SteamLibrary");
        string steamAppsDirectory = Path.Combine(steamDirectory, "steamapps");
        string librarySteamAppsDirectory = Path.Combine(libraryDirectory, "steamapps");
        string gameDirectory = Path.Combine(librarySteamAppsDirectory, "common", "Phasmophobia");
        Directory.CreateDirectory(steamAppsDirectory);
        Directory.CreateDirectory(librarySteamAppsDirectory);
        Directory.CreateDirectory(gameDirectory);
        File.WriteAllText(
            Path.Combine(steamAppsDirectory, "libraryfolders.vdf"),
            $$"""
              "libraryfolders"
              {
                  "0"
                  {
                      "path" "{{EscapeVdfPath(steamDirectory)}}"
                  }
                  "1"
                  {
                      "path" "{{EscapeVdfPath(libraryDirectory)}}"
                  }
              }
              """);
        File.WriteAllText(
            Path.Combine(librarySteamAppsDirectory, "appmanifest_739630.acf"),
            """
            "AppState"
            {
                "appid" "739630"
                "name" "Phasmophobia"
                "installdir" "Phasmophobia"
            }
            """);

        try
        {
            var resolver = new GameDirectoryResolver([steamDirectory]);

            string? result = resolver.Resolve("phasmophobia");

            Assert.Equal(gameDirectory, result);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(steamDirectory)!, recursive: true);
            Directory.Delete(Path.GetDirectoryName(libraryDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void GameDirectoryResolver_WhenSteamGameMatchesMultipleManifests_ReturnsNull()
    {
        string steamDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Steam");
        string steamAppsDirectory = Path.Combine(steamDirectory, "steamapps");
        Directory.CreateDirectory(Path.Combine(steamAppsDirectory, "common", "Phasmophobia"));
        Directory.CreateDirectory(Path.Combine(steamAppsDirectory, "common", "Phasmophobia Demo"));
        File.WriteAllText(
            Path.Combine(steamAppsDirectory, "appmanifest_1.acf"),
            """
            "AppState"
            {
                "name" "Phasmophobia"
                "installdir" "Phasmophobia"
            }
            """);
        File.WriteAllText(
            Path.Combine(steamAppsDirectory, "appmanifest_2.acf"),
            """
            "AppState"
            {
                "name" "Phasmophobia"
                "installdir" "Phasmophobia Demo"
            }
            """);

        try
        {
            var resolver = new GameDirectoryResolver([steamDirectory]);

            string? result = resolver.Resolve("Phasmophobia");

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(steamDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void GameDirectoryResolver_DefaultSteamRootsIncludeSteamDirectoryDirectlyUnderDrive()
    {
        string driveRoot = Path.GetPathRoot(Path.GetTempPath()) ??
                           throw new InvalidOperationException("Temp path must have a drive root.");

        string[] roots = GameDirectoryResolver.CreateDefaultSteamRoots([driveRoot]);

        Assert.Contains(
            Path.Combine(driveRoot, "Steam"),
            roots,
            StringComparer.OrdinalIgnoreCase);
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
                        new AssetFieldPatch(1,
                            [new FieldPatchOperation("m_Name", JsonElementFactory.String("new"))])
                    ]));

            Assert.Equal("--output cannot point to the input assets file.", exception.Message);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    private sealed class RecordingAssetsFileService : IAssetsFileWriter
    {
        public void WritePatch(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
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

    private static string EscapeVdfPath(string path)
    {
        return path.Replace(@"\", @"\\", StringComparison.Ordinal);
    }

    private sealed class CountingEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> _inner;

        public CountingEnumerable(IEnumerable<T> inner)
        {
            _inner = inner;
        }

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return _inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
