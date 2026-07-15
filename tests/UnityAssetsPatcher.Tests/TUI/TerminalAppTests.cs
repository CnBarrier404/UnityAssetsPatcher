using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Tests.Support;
using UnityAssetsPatcher.TUI;
using UnityAssetsPatcher.TUI.Framework;
using Xunit;

namespace UnityAssetsPatcher.Tests.TUI;

public sealed class TerminalAppTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public TerminalAppTests()
    {
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    [Fact]
    public void Run_WhenMainMenuWaitsForInput_HidesCursorUntilApplicationExit()
    {
        TestConsole inner = CreateConsole();
        SelectMainMenuOption(inner, MainMenuOption.Exit);
        var console = new RecordingCursorConsole(inner);
        TerminalApp app = CreateApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        Assert.True(exitCode == 0, inner.Output);
        Assert.Contains(false, console.CursorStates);
        Assert.DoesNotContain(true, console.CursorStates.Take(console.CursorStates.Count - 1));
        Assert.True(console.CursorStates[^1]);
    }

    [Fact]
    public void Run_ChecksForUpdateOnceAndShowsAvailableReleaseOnMainMenu()
    {
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var updateChecker = new StubUpdateChecker(new AvailableUpdate(
            "v2.0.0",
            new Uri("https://example.com/releases/v2.0.0")));
        var assetsFileService = new StubAssetsFileService([]);
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IAssetsAccessScopeFactory>(new TestAssetsAccessScopeFactory(
                assetsFileService,
                assetsFileService))
            .AddUnityAssetsPatcherApplication(Path.Combine(AppContext.BaseDirectory, "backup"))
            .AddUnityAssetsPatcherTUI(
                new AppInfo("Unity Assets Patcher", "v1.0.0"),
                console)
            .AddSingleton<IUpdateChecker>(updateChecker)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        int exitCode = provider.GetRequiredService<TerminalApp>().Run();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, updateChecker.CheckCount);
        Assert.Contains("v2.0.0", console.Output);
        Assert.Contains("https://example.com/releases/v2.0.0", console.Output);
    }

    [Fact]
    public void Run_UsesExplicitAssetsReaderFactoryForWorkflowSessions()
    {
        string zipPath = CreateCameraPatchZip();
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushKey(ConsoleKey.N);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var assetsScopeFactory = new RecordingAssetsScopeFactory(CreateCameraReader(), new StubAssetsFileService([]));
        var app = CreateApp(assetsScopeFactory, console);

        try
        {
            int exitCode = app.Run();

            Assert.Equal(0, exitCode);
            Assert.Equal(2, assetsScopeFactory.CreateScopeCount);
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void Run_WhenSettingsPageWaitsForInput_DoesNotShowCursorBetweenNavigationPages()
    {
        TestConsole inner = CreateConsole();
        SelectMainMenuOption(inner, MainMenuOption.Settings);
        inner.Input.PushKey(ConsoleKey.Escape);
        SelectMainMenuOption(inner, MainMenuOption.Exit);
        var console = new RecordingCursorConsole(inner);
        TerminalApp app = CreateApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        Assert.True(exitCode == 0, inner.Output);
        Assert.True(console.CursorStates.Count(state => !state) >= 2);
        Assert.DoesNotContain(true, console.CursorStates.Take(console.CursorStates.Count - 1));
        Assert.True(console.CursorStates[^1]);
    }

    [Fact]
    public void WritePageHeader_PositionsFooterOnBottomLineAndReturnsToTop()
    {
        TestConsole inner = CreateConsole().Height(24);
        var console = new RecordingCursorConsole(inner);
        var ui = new TerminalUI(console, new AppInfo("Unity Assets Patcher", "dev"));

        ui.Layout.ShowPage("Main menu", shortcutHint: "Shortcuts");

        Assert.Contains((1, 24), console.CursorPositions);
        Assert.Equal((1, 1), console.CursorPositions[^1]);
    }

    [Fact]
    public void WriteBottomFooterHint_PreparesCleanLineAboveFooter()
    {
        TestConsole inner = CreateConsole().Width(20).Height(24);
        var console = new RecordingCursorConsole(inner);
        var ui = new TerminalUI(console, new AppInfo("Unity Assets Patcher", "dev"));

        ui.Layout.WriteBottomFooterHint("Shortcuts");

        Assert.Equal([(1, 24), (1, 23), (1, 22), (1, 22)], console.CursorPositions);
        Assert.EndsWith(new string(' ', 40), inner.Output);
    }

    [Fact]
    public void ClearBottomFooterArea_ClearsFooterSpacerAndContentLine()
    {
        TestConsole inner = CreateConsole().Width(20).Height(24);
        var console = new RecordingCursorConsole(inner);
        var ui = new TerminalUI(console, new AppInfo("Unity Assets Patcher", "dev"));

        ui.Layout.ClearBottomFooterArea();

        Assert.Equal([(1, 24), (1, 23), (1, 22), (1, 22)], console.CursorPositions);
        Assert.Contains("\e[s", inner.Output);
        Assert.Contains("\e[u", inner.Output);
        Assert.EndsWith("\e[u", inner.Output);
    }

    [Fact]
    public void ReadExistingFilePath_WhenValueIsQ_TreatsInputAsAPath()
    {
        string assetsPath = CreateTempFile(".assets");
        TestConsole console = CreateConsole();
        console.Input.PushTextWithEnter("q");
        console.Input.PushTextWithEnter(assetsPath);
        var prompts = new TerminalPrompts(console, new TerminalText(console));

        try
        {
            string? path = prompts.ReadExistingFilePath("Assets file path");

            Assert.Equal(assetsPath, path);
            Assert.Contains("File not found: q", console.Output);
        }
        finally
        {
            File.Delete(assetsPath);
        }
    }

    [Fact]
    public void Run_WhenInstallPromptReceivesEscape_ReturnsToMainMenu()
    {
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushKey(ConsoleKey.Escape);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        TerminalApp app = CreateApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        string text = console.Output;
        Assert.True(exitCode == 0, console.Output);
        Assert.Contains("Install Mod", text);
        Assert.Contains("Mod zip path", text);
        Assert.DoesNotContain("Game directory", text);
    }

    [Fact]
    public void Run_WhenInstallPageIsCanceled_PrintsDryRunSummaryWithoutInstalling()
    {
        string zipPath = CreateCameraPatchZip();
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        string targetPath = Path.Combine(gameDirectory, "Game_Data", "sharedassets0.assets");
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushKey(ConsoleKey.N);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        TerminalApp app = CreateApp(CreateCameraReader(), console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Install Mod", text);
            Assert.Contains("PREVIEW", text);
            Assert.Contains("Apply these changes?", text);
            Assert.Contains("Install canceled.", text);
            Assert.DoesNotContain("INSTALLED", text);
            Assert.Contains("Test Mod", text);
            Assert.Contains("UnityAssetsPatcher.Tests", text);
            Assert.Contains("1.0.0", text);
            Assert.Equal(1, CountOccurrences(text, "Version"));
            Assert.DoesNotContain("Test Mod 1.0.0", text);
            Assert.Contains("sharedassets0.assets", text);
            Assert.Contains("- sharedassets0.assets:", text);
            Assert.DoesNotContain("Operations", text);
            Assert.DoesNotContain("field of view", text);
            Assert.DoesNotContain("90.0 -> 75.0", text);
            Assert.DoesNotContain("Read package", text);
            Assert.Equal("original", File.ReadAllText(targetPath));
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void Run_WhenInstallPreviewWaitsForConfirmation_RedrawsShortcutHintAbovePrompt()
    {
        const string shortcutHint = "Shortcuts: ↑/↓ to choose | Esc to cancel | Ctrl + C to exit";
        string zipPath = CreateCameraPatchZip();
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        TestConsole inner = CreateConsole().Height(10);
        SelectMainMenuOption(inner, MainMenuOption.InstallMod);
        inner.Input.PushTextWithEnter(zipPath);
        inner.Input.PushTextWithEnter(gameDirectory);
        inner.Input.PushKey(ConsoleKey.N);
        ReturnToMainMenu(inner);
        SelectMainMenuOption(inner, MainMenuOption.Exit);
        var console = new RecordingCursorConsole(inner);
        TerminalApp app = CreateApp(CreateCameraReader(), console);

        try
        {
            int exitCode = app.Run();

            Assert.True(exitCode == 0, inner.Output);
            Assert.Contains((1, 10), console.CursorPositions);
            Assert.Contains((1, 9), console.CursorPositions);
            Assert.Contains((1, 8), console.CursorPositions);
            Assert.True(CountOccurrences(inner.Output, shortcutHint) >= 2);
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void Run_WhenInstallPageIsConfirmed_PreviewsAndInstallsMod()
    {
        string zipPath = CreateCameraPatchZip();
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        string targetPath = Path.Combine(gameDirectory, "Game_Data", "sharedassets0.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushKey(ConsoleKey.Y);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        TerminalApp app = CreateApp(CreateCameraReader(), backupDirectory, console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Install Mod", text);
            Assert.Contains("PREVIEW", text);
            Assert.Contains("Apply these changes?", text);
            Assert.Contains("Apply these changes? y/N", text);
            Assert.DoesNotContain("[y/N] [y/n]", text);
            Assert.Contains("INSTALLED", text);
            Assert.Contains("Operations", text);
            Assert.Contains("Test Mod", text);
            Assert.Contains("1.0.0", text);
            Assert.Equal(2, CountOccurrences(text, "Version"));
            Assert.DoesNotContain("Test Mod 1.0.0", text);
            Assert.Contains("Backup", text);
            Assert.DoesNotContain("field of view", text);
            Assert.DoesNotContain("90.0 -> 75.0", text);
            Assert.DoesNotContain("Read package", text);
            Assert.Equal("patched", File.ReadAllText(targetPath));
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    [Fact]
    public void Run_WhenUninstallPageIsConfirmed_RestoresInstalledModFromRecord()
    {
        string zipPath = CreateCameraPatchZip();
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        string targetPath = Path.Combine(gameDirectory, "Game_Data", "sharedassets0.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushKey(ConsoleKey.Y);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.UninstallMod);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushKey(ConsoleKey.Y);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        TerminalApp app = CreateApp(CreateCameraReader(), backupDirectory, console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Uninstall Mod", text);
            Assert.Contains("Test Mod", text);
            Assert.Contains("UNINSTALL PREVIEW", text);
            Assert.Contains("UNINSTALLED", text);
            Assert.Equal("original", File.ReadAllText(targetPath));
            Assert.Equal(
                [Path.Combine(backupDirectory, ".operations.lock")],
                Directory.EnumerateFileSystemEntries(backupDirectory));
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    [Fact]
    public void Run_InspectListShowsAssetsAndTruncationSummary()
    {
        string assetsFilePath = Path.GetTempFileName();
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InspectAssets);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter(assetsFilePath);
        console.Input.PushKey(ConsoleKey.Enter);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        AssetsInfo[] assets = Enumerable.Range(1, 105)
            .Select(id => new AssetsInfo(id, "Camera"))
            .ToArray();
        var fieldTrees = assets.ToDictionary(
            asset => asset.PathId,
            asset => new AssetsFieldInfo(
                "Base",
                asset.TypeName,
                null,
                [new AssetsFieldInfo("m_Name", "string", $"Asset Name {asset.PathId}", [])]));
        TerminalApp app = CreateApp(new StubAssetsFileService(assets, fieldTrees), console);

        try
        {
            int exitCode = app.Run();

            Assert.Equal(0, exitCode);
            Assert.Contains("Inspect Assets", console.Output);
            Assert.Contains("Path ID", console.Output);
            Assert.Contains("Name", console.Output);
            Assert.Contains("Camera", console.Output);
            Assert.Contains("Asset Name 1", console.Output);
            Assert.Contains("Showing 100 of 105 assets.", console.Output);
        }
        finally
        {
            File.Delete(assetsFilePath);
        }
    }

    [Fact]
    public void Run_WhenSettingsToggleVerboseLogging_InstallPreviewPrintsFieldDiff()
    {
        string zipPath = CreateCameraPatchZip();
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.Settings);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Escape);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushKey(ConsoleKey.N);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        TerminalApp app = CreateApp(CreateCameraReader(), console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Settings", text);
            Assert.Contains("[X] Verbose output", text);
            Assert.Contains("Shortcuts: ↑/↓ to choose | Space to toggle | Esc to cancel | Ctrl + C to exit", text);
            Assert.DoesNotContain("Enter or Esc", text);
            Assert.Contains(
                "Show detailed install preview logs and per-stage install timings.",
                FirstLineContaining(text, "Verbose output"));
            Assert.Contains("field of view", text);
            Assert.Contains("90 -> 75.0", text);
            Assert.Contains("Read package", text);
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void Run_WhenOptionalContentAccepted_InstallsBaseAndOptional()
    {
        string zipPath = CreateOptionalContentZip();
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        string optionalTargetPath = Path.Combine(gameDirectory, "Game_Data", "sharedassets1.assets");
        File.WriteAllText(optionalTargetPath, "original");
        string baseTargetPath = Path.Combine(gameDirectory, "Game_Data", "sharedassets0.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Y);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        TerminalApp app = CreateApp(CreateCameraReader(), backupDirectory, console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("This mod includes optional content:", text);
            Assert.Contains("Bonus content", text);
            Assert.Contains("INSTALLED", text);
            Assert.Contains("Optional content", text);
            Assert.Equal("patched", File.ReadAllText(baseTargetPath));
            Assert.Equal("patched", File.ReadAllText(optionalTargetPath));
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    private enum MainMenuOption
    {
        InstallMod,
        UninstallMod,
        InspectAssets,
        Settings,
        Exit,
    }

    private sealed class RecordingAssetsScopeFactory : IAssetsAccessScopeFactory
    {
        private readonly IAssetsFileReader _assetsReader;
        private readonly IAssetsFileWriter _assetsWriter;

        public RecordingAssetsScopeFactory(IAssetsFileReader assetsReader, IAssetsFileWriter assetsWriter)
        {
            _assetsReader = assetsReader;
            _assetsWriter = assetsWriter;
        }

        public int CreateScopeCount { get; private set; }

        public IAssetsAccessScope CreateScope()
        {
            CreateScopeCount++;
            return new TestAssetsAccessScope(_assetsReader, _assetsWriter);
        }
    }

    private static TestConsole CreateConsole(bool supportsAnsi = true)
    {
        return new TestConsole()
            .Interactive()
            .SupportsAnsi(supportsAnsi)
            .SupportsUnicode(false)
            .Width(120);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int startIndex = 0;

        while (true)
        {
            int index = text.IndexOf(value, startIndex, StringComparison.Ordinal);

            if (index < 0)
            {
                return count;
            }

            count++;
            startIndex = index + value.Length;
        }
    }

    private static string FirstLineContaining(string text, string value)
    {
        return text
            .ReplaceLineEndings("\n")
            .Split('\n')
            .First(line => line.Contains(value, StringComparison.Ordinal));
    }

    private static void SelectMainMenuOption(TestConsole console, MainMenuOption option)
    {
        if (option is MainMenuOption.Exit)
        {
            console.Input.PushKey(ConsoleKey.Escape);
            return;
        }

        SelectSubMenuOption(console, (int)option);
    }

    private static void SelectSubMenuOption(TestConsole console, int zeroBasedIndex)
    {
        for (int i = 0; i < zeroBasedIndex; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
    }

    private static void ReturnToMainMenu(TestConsole console)
    {
        console.Input.PushKey(ConsoleKey.Enter);
    }

    private static TerminalApp CreateApp(StubAssetsFileService assetsFileService, IAnsiConsole console)
    {
        return CreateApp(
            new TestAssetsAccessScopeFactory(assetsFileService, assetsFileService),
            Path.Combine(AppContext.BaseDirectory, "backup"),
            console);
    }

    private static TerminalApp CreateApp(
        StubAssetsFileService assetsFileService,
        string backupDirectory,
        IAnsiConsole console)
    {
        return CreateApp(
            new TestAssetsAccessScopeFactory(assetsFileService, assetsFileService),
            backupDirectory,
            console);
    }

    private static TerminalApp CreateApp(
        IAssetsAccessScopeFactory assetsScopeFactory,
        IAnsiConsole console)
    {
        return CreateApp(
            assetsScopeFactory,
            Path.Combine(AppContext.BaseDirectory, "backup"),
            console);
    }

    private static TerminalApp CreateApp(
        IAssetsAccessScopeFactory assetsScopeFactory,
        string backupDirectory,
        IAnsiConsole console)
    {
        return new ServiceCollection()
            .AddSingleton<IAssetsAccessScopeFactory>(assetsScopeFactory)
            .AddUnityAssetsPatcherApplication(backupDirectory)
            .AddUnityAssetsPatcherTUI(
                new AppInfo("Unity Assets Patcher", "dev"),
                console)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            })
            .GetRequiredService<TerminalApp>();
    }

    private sealed class StubUpdateChecker(AvailableUpdate? update) : IUpdateChecker
    {
        public int CheckCount { get; private set; }

        public AvailableUpdate? CheckForUpdate()
        {
            CheckCount++;
            return update;
        }
    }

    private sealed class TestAssetsAccessScopeFactory(
        IAssetsFileReader assetsReader,
        IAssetsFileWriter assetsWriter) : IAssetsAccessScopeFactory
    {
        public IAssetsAccessScope CreateScope()
        {
            return new TestAssetsAccessScope(assetsReader, assetsWriter);
        }
    }

    private sealed class TestAssetsAccessScope(
        IAssetsFileReader assetsReader,
        IAssetsFileWriter assetsWriter) : IAssetsAccessScope
    {
        public IAssetsFileReader Reader => assetsReader;
        public IAssetsFileWriter Writer => assetsWriter;

        public void CloseReadSessions()
        {
            assetsReader.CloseReadSessions();
        }

        public void Dispose()
        {
            if (assetsReader is IDisposable disposableReader)
            {
                disposableReader.Dispose();
            }

            if (!ReferenceEquals(assetsReader, assetsWriter) && assetsWriter is IDisposable disposableWriter)
            {
                disposableWriter.Dispose();
            }
        }
    }

    private static string CreateCameraPatchZip()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "patches": [
                {
                  "target": "sharedassets0.assets",
                  "type": "Camera",
                  "include": [
                    {
                      "field of view": 90.0
                    }
                  ],
                  "set": [
                    {
                      "field": "field of view",
                      "from": 90.0,
                      "to": 75.0
                    }
                  ]
                }
              ]
            }
            """);
        return zipPath;
    }

    private static string CreateOptionalContentZip()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    { "type": "Camera", "match": { "field of view": 90.0 }, "set": { "field of view": { "from": 90.0, "to": 75.0 } } }
                  ]
                }
              ],
              "optional": [
                {
                  "name": "Bonus content",
                  "description": "Adds extra stuff",
                  "targets": [
                    {
                      "file": "sharedassets1.assets",
                      "patches": [
                        { "type": "Camera", "match": { "field of view": 90.0 }, "set": { "field of view": { "from": 90.0, "to": 75.0 } } }
                      ]
                    }
                  ]
                }
              ]
            }
            """);
        return zipPath;
    }

    private static StubAssetsFileService CreateCameraReader()
    {
        return new StubAssetsFileService(
            [new AssetsInfo(50, "Camera")],
            new Dictionary<long, AssetsFieldInfo>
            {
                [50] = CameraFieldTree("90.0"),
            });
    }

    private static AssetsFieldInfo CameraFieldTree(string fieldOfView)
    {
        return new AssetsFieldInfo(
            "Camera",
            "Camera",
            null,
            [new AssetsFieldInfo("field of view", "float", fieldOfView, [])]);
    }

    private static string CreateGameDirectory(string assetsFileName)
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, assetsFileName), "original");

        return gameDirectory;
    }

    private static string CreateTempFile(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        File.WriteAllText(path, "original");

        return path;
    }

    private sealed class RecordingCursorConsole : IAnsiConsole
    {
        private readonly TestConsole _inner;

        public RecordingCursorConsole(TestConsole inner)
        {
            _inner = inner;
            Cursor = new RecordingCursor(inner.Cursor, CursorStates, CursorPositions);
        }

        public List<bool> CursorStates { get; } = [];
        public List<(int Column, int Line)> CursorPositions { get; } = [];

        public Profile Profile => _inner.Profile;
        public IAnsiConsoleCursor Cursor { get; }
        public IAnsiConsoleInput Input => _inner.Input;
        public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
        public RenderPipeline Pipeline => _inner.Pipeline;

        public void Clear(bool home)
        {
            _inner.Clear(home);
        }

        public void Write(IRenderable renderable)
        {
            _inner.Write(renderable);
        }

        public void WriteAnsi(Action<AnsiWriter> action)
        {
            _inner.WriteAnsi(action);
        }
    }

    private sealed class RecordingCursor : IAnsiConsoleCursor
    {
        private readonly IAnsiConsoleCursor _inner;
        private readonly List<bool> _states;
        private readonly List<(int Column, int Line)> _positions;

        public RecordingCursor(
            IAnsiConsoleCursor inner,
            List<bool> states,
            List<(int Column, int Line)> positions)
        {
            _inner = inner;
            _states = states;
            _positions = positions;
        }

        public void Show(bool show)
        {
            _states.Add(show);
            _inner.Show(show);
        }

        public void SetPosition(int column, int line)
        {
            _positions.Add((column, line));
            _inner.SetPosition(column, line);
        }

        public void Move(CursorDirection direction, int steps)
        {
            _inner.Move(direction, steps);
        }
    }
}
