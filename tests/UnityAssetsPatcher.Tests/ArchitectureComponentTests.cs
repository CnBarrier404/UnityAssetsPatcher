using System.Text.Json;
using System.Xml.Linq;
using UnityAssetsPatcher.AssetsTools;
using Xunit;
using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Tui;

namespace UnityAssetsPatcher.Tests;

public sealed class ArchitectureComponentTests
{
    [Fact]
    public void ProductionTypes_AreSplitAcrossExpectedAssemblies()
    {
        Assert.Equal("UnityAssetsPatcher.Core", typeof(AssetsInfo).Assembly.GetName().Name);
        Assert.Equal("UnityAssetsPatcher.Application", typeof(PatchAssetsWorkflow).Assembly.GetName().Name);
        Assert.Equal("UnityAssetsPatcher.Application", typeof(ModManifest).Assembly.GetName().Name);
        Assert.Equal("UnityAssetsPatcher.AssetsTools", typeof(AssetsFileService).Assembly.GetName().Name);
        Assert.Equal("UnityAssetsPatcher", typeof(TerminalApp).Assembly.GetName().Name);
    }

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
    public void ApplicationImplementationTypes_ArePublicForDirectTestAccess()
    {
        string[] internalNamespaces =
        [
            "UnityAssetsPatcher.Application.Installing",
            "UnityAssetsPatcher.Application.Manifests",
            "UnityAssetsPatcher.Application.Patching",
        ];
        var implementationTypes = typeof(AssetsWorkflowService).Assembly.GetTypes()
            .Where(type =>
                !type.IsNested &&
                (type.Namespace is "UnityAssetsPatcher.Application.Workflows" &&
                 type != typeof(AssetsWorkflowService) ||
                 type.Namespace is not null &&
                 internalNamespaces.Any(ns => type.Namespace.StartsWith(ns, StringComparison.Ordinal))))
            .ToArray();

        Assert.All(implementationTypes, type => Assert.True(type.IsPublic, type.FullName));

        Assert.True(typeof(AssetsWorkflowService).IsPublic);
        Assert.True(typeof(PatchPreviewResult).IsPublic);
        Assert.True(typeof(ModManifest).IsPublic);
    }

    [Fact]
    public void ModManifestLoader_IsTheDefaultManifestLoaderImplementation()
    {
        Assert.True(typeof(IModManifestLoader).IsAssignableFrom(typeof(ModManifestLoader)));

        string source = ReadSourceTree("src", "UnityAssetsPatcher.Application");

        Assert.DoesNotContain("FileModManifestLoader", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationPatchingSource_DoesNotDependOnWorkflows()
    {
        string patchingDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher.Application",
            "Patching");

        foreach (string file in Directory.EnumerateFiles(patchingDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);

            Assert.DoesNotContain("UnityAssetsPatcher.Application.Workflows", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Workflows.", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ApplicationWorkflowSource_DoesNotCallStaticManifestLoader()
    {
        string source = ReadSourceTree("src", "UnityAssetsPatcher.Application", "Workflows");

        Assert.DoesNotContain("ModManifestLoader.Load(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ConcreteApplicationWorkflows_DoNotCreateManifestLoaderImplementation()
    {
        string workflowsDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher.Application",
            "Workflows");
        string[] workflowFiles =
        [
            "FindAssetsWorkflow.cs",
            "PatchAssetsWorkflow.cs",
            "InstallModWorkflow.cs",
        ];

        foreach (string file in workflowFiles)
        {
            string source = File.ReadAllText(Path.Combine(workflowsDirectory, file));

            Assert.DoesNotContain("new ModManifestLoader(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ManifestJsonReader", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TerminalSource_UsesAssetsWorkflowServiceFacade()
    {
        string source = ReadSourceTree("src", "UnityAssetsPatcher");

        Assert.Contains("AssetsWorkflowService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AssetsWorkflowSet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("context.Workflows", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalSource_DoesNotDependOnApplicationImplementationNamespaces()
    {
        string source = ReadSourceTree("src", "UnityAssetsPatcher");

        Assert.DoesNotContain("UnityAssetsPatcher.Application.Patching", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityAssetsPatcher.Application.Installing", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityAssetsPatcher.Application.Manifests", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModManifestLoader", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PatchPlanBuilder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstallTargetResolver", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalSource_DoesNotUseCommandLineParser()
    {
        string source = ReadSourceTree("src", "UnityAssetsPatcher");

        Assert.DoesNotContain("System.CommandLine", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityAssetsPatcher.Cli", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandCatalog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ICommandModule", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalSource_SplitsInteractivePagesIntoDedicatedTypes()
    {
        string terminalAppSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Tui",
            "TerminalApp.cs"));
        string terminalSource = ReadSourceTree("src", "UnityAssetsPatcher");

        Assert.Contains("class InstallTerminalPage", terminalSource, StringComparison.Ordinal);
        Assert.Contains("class InspectTerminalPage", terminalSource, StringComparison.Ordinal);
        Assert.Contains("class FindTerminalPage", terminalSource, StringComparison.Ordinal);
        Assert.Contains("class PatchTerminalPage", terminalSource, StringComparison.Ordinal);
        Assert.Contains("class SettingsTerminalPage", terminalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunInstall(", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunInspect(", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunFind(", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunPatch(", terminalAppSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalApp_UsesTerminalPageCollectionForMainMenuDispatch()
    {
        string terminalAppSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Tui",
            "TerminalApp.cs"));

        Assert.Contains("IReadOnlyList<TerminalPage>", terminalAppSource, StringComparison.Ordinal);
        Assert.Contains("new InstallTerminalPage", terminalAppSource, StringComparison.Ordinal);
        Assert.Contains("new InspectTerminalPage", terminalAppSource, StringComparison.Ordinal);
        Assert.Contains("new FindTerminalPage", terminalAppSource, StringComparison.Ordinal);
        Assert.Contains("new PatchTerminalPage", terminalAppSource, StringComparison.Ordinal);
        Assert.Contains("new SettingsTerminalPage", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TerminalPageFactory", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("choice switch", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteMainHeader", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteMainMenu", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TerminalOutputFormatter.WriteMainMenu", terminalAppSource, StringComparison.Ordinal);
        Assert.Contains("MainMenuTerminalPage", terminalAppSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalApp_DoesNotCreateWorkflowServiceScopes()
    {
        string terminalAppSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Tui",
            "TerminalApp.cs"));

        Assert.DoesNotContain("AssetsWorkflowService", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IAssetsReadScopeFactory", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateServiceScopeFactory", terminalAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TerminalAssetsWorkflowServiceScope", terminalAppSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalSource_LivesUnderDedicatedTuiFolderAndNamespace()
    {
        string root = FindRepositoryRoot();
        string projectDirectory = Path.Combine(root, "src", "UnityAssetsPatcher");
        string tuiDirectory = Path.Combine(projectDirectory, "Tui");
        string pagesDirectory = Path.Combine(tuiDirectory, "Pages");
        string[] tuiRootFiles =
        [
            "TerminalApp.cs",
            "MainMenuTerminalPage.cs",
            "InteractivePrompts.cs",
            "TerminalPageLayout.cs",
            "TerminalOutputFormatter.cs",
        ];
        string[] pageFiles =
        [
            "InstallTerminalPage.cs",
            "InspectTerminalPage.cs",
            "FindTerminalPage.cs",
            "PatchTerminalPage.cs",
            "SettingsTerminalPage.cs",
        ];

        foreach (string file in tuiRootFiles)
        {
            string path = Path.Combine(tuiDirectory, file);

            Assert.True(File.Exists(path), $"{file} should live under the Tui folder.");
            Assert.Contains("namespace UnityAssetsPatcher.Tui;", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(projectDirectory, file)), $"{file} should not live at project root.");
        }

        foreach (string file in pageFiles)
        {
            string path = Path.Combine(pagesDirectory, file);

            Assert.True(File.Exists(path), $"{file} should live under the Tui/Pages folder.");
            Assert.Contains("namespace UnityAssetsPatcher.Tui.Pages;", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tuiDirectory, file)), $"{file} should not live at the Tui root.");
        }
    }

    [Fact]
    public void TerminalPages_UseSharedPageLayoutForPageFrames()
    {
        string tuiDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Tui");
        string pagesDirectory = Path.Combine(tuiDirectory, "Pages");
        string terminalPageSource = File.ReadAllText(Path.Combine(tuiDirectory, "TerminalPage.cs"));
        string[] files =
        [
            "InstallTerminalPage.cs",
            "InspectTerminalPage.cs",
            "FindTerminalPage.cs",
            "PatchTerminalPage.cs",
            "SettingsTerminalPage.cs",
        ];

        Assert.Contains("Layout.ShowPage", terminalPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WritePageHeader", terminalPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteApplicationHeader", terminalPageSource, StringComparison.Ordinal);

        foreach (string file in files)
        {
            string source = File.ReadAllText(Path.Combine(pagesDirectory, file));

            Assert.Contains("NewPage(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Layout.ShowPage", source, StringComparison.Ordinal);
            Assert.DoesNotContain("WritePageHeader", source, StringComparison.Ordinal);
            Assert.DoesNotContain("WriteApplicationHeader", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TerminalPages_DefineMetadataAndUseSharedNewPageHelper()
    {
        string tuiDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Tui");
        string pagesDirectory = Path.Combine(tuiDirectory, "Pages");
        string terminalPageSource = File.ReadAllText(Path.Combine(tuiDirectory, "TerminalPage.cs"));
        string[] pageFiles =
        [
            "InstallTerminalPage.cs",
            "InspectTerminalPage.cs",
            "FindTerminalPage.cs",
            "PatchTerminalPage.cs",
            "SettingsTerminalPage.cs",
        ];

        Assert.Contains("abstract class TerminalPage", terminalPageSource, StringComparison.Ordinal);
        Assert.Contains("protected void NewPage", terminalPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TerminalMenuItem", terminalPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MenuItem", terminalPageSource, StringComparison.Ordinal);
        Assert.Contains("public abstract string Title", terminalPageSource, StringComparison.Ordinal);
        Assert.Contains("public abstract string Description", terminalPageSource, StringComparison.Ordinal);

        foreach (string file in pageFiles)
        {
            string source = File.ReadAllText(Path.Combine(pagesDirectory, file));

            Assert.Contains(": TerminalPage", source, StringComparison.Ordinal);
            Assert.Contains("public override string Title", source, StringComparison.Ordinal);
            Assert.Contains("public override string Description", source, StringComparison.Ordinal);
            Assert.Contains("NewPage(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TerminalMenuItem", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MenuItem", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Layout.ShowPage", source, StringComparison.Ordinal);
        }
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

    private static string ReadSourceTree(params string[] pathSegments)
    {
        string directory = Path.Combine([FindRepositoryRoot(), .. pathSegments]);

        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                   StringComparison.Ordinal) &&
                               !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                   StringComparison.Ordinal))
                .Select(File.ReadAllText));
    }
}
