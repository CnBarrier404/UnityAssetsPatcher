using System.Xml.Linq;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ApplicationArchitectureTests
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
    public void WorkflowModules_DoNotDependOnWorkflowNamespace()
    {
        string root = FindRepositoryRoot();
        string modulesDirectory = Path.Combine(
            root,
            "src",
            "UnityAssetsPatcher.Application",
            "Modules");

        string[] offenders = Directory
            .EnumerateFiles(modulesDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path)
                .Contains("UnityAssetsPatcher.Application.Workflows", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void WorkflowModules_DoNotUseInstallSpecificFileNames()
    {
        string root = FindRepositoryRoot();
        string modulesDirectory = Path.Combine(
            root,
            "src",
            "UnityAssetsPatcher.Application",
            "Modules");

        string[] offenders = Directory
            .EnumerateFiles(modulesDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
            .Where(name => name.StartsWith("Install", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void WorkflowModules_DoNotExposeInstallSpecificResultContracts()
    {
        string root = FindRepositoryRoot();
        string modulesDirectory = Path.Combine(
            root,
            "src",
            "UnityAssetsPatcher.Application",
            "Modules");
        string[] forbiddenTokens =
        [
            "InstallModFileResult",
            "InstallCopiedFileResult",
            "InstallPreviewFileResult",
            "InstallCopyFilePreviewResult",
            "InstallTimingResult",
        ];

        string[] offenders = Directory
            .EnumerateFiles(modulesDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                string source = File.ReadAllText(path);

                return forbiddenTokens.Any(token => source.Contains(token, StringComparison.Ordinal));
            })
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void WorkflowModules_UseCohesiveModuleFiles()
    {
        string root = FindRepositoryRoot();
        string modulesDirectory = Path.Combine(
            root,
            "src",
            "UnityAssetsPatcher.Application",
            "Modules");
        string[] expectedFiles =
        [
            "GameDirectoryResolver.cs",
            "ManifestTargetSelector.cs",
            "ManifestPatchOperationValidator.cs",
            "PackageArchive.cs",
            "PackageSource.cs",
            "PackageSourceLoader.cs",
            "PackageWorkspace.cs",
            "PatchAssetApplier.cs",
            "PatchPlanner.cs",
            "PayloadCopier.cs",
            "PayloadPlanner.cs",
            "TargetAsset.cs",
            "TargetAssetResolver.cs",
            "TargetAssetSet.cs",
            "WorkflowTiming.cs",
        ];

        string[] actualFiles = Directory
            .EnumerateFiles(modulesDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(modulesDirectory, path)
                .Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedFiles.OrderBy(name => name, StringComparer.Ordinal), actualFiles);
    }

    [Fact]
    public void Patching_UsesCohesiveBoundaryFiles()
    {
        string root = FindRepositoryRoot();
        string patchingDirectory = Path.Combine(
            root,
            "src",
            "UnityAssetsPatcher.Application",
            "Patching");
        string[] expectedFiles =
        [
            "AssetQueryService.cs",
            "FieldPatchPlanBuilder.cs",
            "PatchOperationRules.cs",
            "PatchOutputWriter.cs",
            "PatchPlanBuilder.cs",
            "ReplacementPlanBuilder.cs",
        ];

        string[] actualFiles = Directory
            .EnumerateFiles(patchingDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;

        Assert.Equal(expectedFiles.OrderBy(name => name, StringComparer.Ordinal), actualFiles);
    }

    [Fact]
    public void InstallWorkflow_DoesNotAddInstallSpecificSubWorkflows()
    {
        string root = FindRepositoryRoot();
        string workflowsDirectory = Path.Combine(
            root,
            "src",
            "UnityAssetsPatcher.Application",
            "Workflows");

        string[] offenders = Directory
            .EnumerateFiles(workflowsDirectory, "*Install*Workflow.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), "InstallModWorkflow.cs", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void InstallWorkflow_ReusesPatchWorkflowForPatchSteps()
    {
        string root = FindRepositoryRoot();
        string installWorkflowPath = Path.Combine(
            root,
            "src",
            "UnityAssetsPatcher.Application",
            "Workflows",
            "InstallModWorkflow.cs");

        string source = File.ReadAllText(installWorkflowPath);

        Assert.Contains("PatchAssetsWorkflow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new PatchPlanner", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new PatchAssetApplier", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallWorkflow_DelegatesPatchOperationValidationToInstallModule()
    {
        string root = FindRepositoryRoot();
        string installWorkflowPath = Path.Combine(
            root,
            "src",
            "UnityAssetsPatcher.Application",
            "Workflows",
            "InstallModWorkflow.cs");

        string source = File.ReadAllText(installWorkflowPath);

        Assert.DoesNotContain("PatchOperationRules", source, StringComparison.Ordinal);
        Assert.Contains("ManifestPatchOperationValidator", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowModules_ReturnTypedStepOutputs()
    {
        Assert.Equal(
            typeof(PackageSource),
            typeof(PackageSourceLoader).GetMethod(nameof(PackageSourceLoader.Execute))?.ReturnType);
        Assert.Equal(
            typeof(TargetAssetSet),
            typeof(TargetAssetResolver).GetMethod(nameof(TargetAssetResolver.Execute))?.ReturnType);
        Assert.Equal(
            typeof(PayloadPlan),
            typeof(PayloadPlanner).GetMethod(nameof(PayloadPlanner.Plan))?.ReturnType);
        Assert.Equal(
            typeof(PayloadPreview),
            typeof(PayloadPlanner).GetMethod(nameof(PayloadPlanner.Preview))?.ReturnType);
        Assert.Equal(
            typeof(PayloadCopyResult),
            typeof(PayloadCopier).GetMethod(nameof(PayloadCopier.Execute))?.ReturnType);
        Assert.Equal(
            typeof(PatchAssetPreview),
            typeof(PatchPlanner).GetMethod(nameof(PatchPlanner.Preview))?.ReturnType);
        Assert.Equal(
            typeof(PatchAssetPlan),
            typeof(PatchPlanner).GetMethod(nameof(PatchPlanner.Plan))?.ReturnType);
        Assert.Equal(
            typeof(PatchAssetApplyResult),
            typeof(PatchAssetApplier).GetMethod(nameof(PatchAssetApplier.Execute))?.ReturnType);
        Assert.Equal(
            typeof(WorkflowTimingSnapshot),
            typeof(WorkflowTiming).GetMethod(nameof(WorkflowTiming.Build))?.ReturnType);
    }

    [Fact]
    public void TargetAssetResolver_WhenTargetMatchesMultipleFiles_Throws()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string firstDirectory = Path.Combine(gameDirectory, "Game_Data");
        string secondDirectory = Path.Combine(gameDirectory, "Backup_Data");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        File.WriteAllText(Path.Combine(firstDirectory, "sharedassets0.assets"), "original");
        File.WriteAllText(Path.Combine(secondDirectory, "sharedassets0.assets"), "original");
        ModManifest manifest = CreateTargetManifest("sharedassets0.assets");

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new TargetAssetResolver().Execute(gameDirectory, manifest, new WorkflowTiming()));

            Assert.Contains("matched multiple files", exception.Message);
        }
        finally
        {
            Directory.Delete(gameDirectory, recursive: true);
        }
    }

    [Fact]
    public void TargetAssetResolver_WhenMultipleTargetsAreProvided_ResolvesAllTargets()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string dataDirectory = Path.Combine(gameDirectory, "Game_Data");
        Directory.CreateDirectory(dataDirectory);
        string resourcesPath = Path.Combine(dataDirectory, "resources.assets");
        string sharedAssetsPath = Path.Combine(dataDirectory, "sharedassets0.assets");
        File.WriteAllText(resourcesPath, "original");
        File.WriteAllText(sharedAssetsPath, "original");
        File.WriteAllText(Path.Combine(dataDirectory, "globalgamemanagers"), "original");
        var manifest = new ModManifest(
            "Test Mod",
            "UnityAssetsPatcher.Tests",
            "1.0.0",
            null,
            null,
            [],
            [
                CreatePatch("resources.assets"),
                CreatePatch("sharedassets0.assets"),
            ]);

        try
        {
            TargetAssetSet result = new TargetAssetResolver()
                .Execute(gameDirectory, manifest, new WorkflowTiming());

            Assert.Equal(2, result.Targets.Count);
            Assert.Contains(result.Targets, target =>
                target.Name == "resources.assets" && target.AssetsFilePath == Path.GetFullPath(resourcesPath));
            Assert.Contains(result.Targets, target =>
                target.Name == "sharedassets0.assets" && target.AssetsFilePath == Path.GetFullPath(sharedAssetsPath));
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

    private static ModManifest CreateTargetManifest(string target)
    {
        return new ModManifest(
            "Test Mod",
            "UnityAssetsPatcher.Tests",
            "1.0.0",
            null,
            null,
            [],
            [CreatePatch(target)]);
    }

    private static ManifestPatch CreatePatch(string target)
    {
        return new ManifestPatch(
            target,
            "Camera",
            [],
            null,
            null);
    }
}
