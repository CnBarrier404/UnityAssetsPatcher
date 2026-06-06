using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Tui;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class TerminalAppTests
{
    [Fact]
    public void Run_WhenStarted_PrintsSpectreMenuWithAllPages()
    {
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        string text = console.Output;
        Assert.True(exitCode == 0, console.Output);
        Assert.Equal(1, CountOccurrences(text, "Unity Assets Patcher"));
        Assert.DoesNotContain("| Unity Assets Patcher |", text);
        Assert.DoesNotContain("Inspect, find, patch, and install Unity assets file mods.", text);
        AssertTitleIsInsideCompactPanel(text);
        Assert.Contains("Unity Assets Patcher", text);
        Assert.Contains("Install Mod", text);
        Assert.DoesNotContain("Install mod package", text);
        Assert.DoesNotContain("Preview a mod install", text);
        Assert.DoesNotContain("Preview install", text);
        Assert.DoesNotContain("Apply install", text);
        Assert.Contains("Inspect assets", text);
        Assert.Contains("Find assets", text);
        Assert.DoesNotContain("Patch assets", text);
        Assert.Contains("Settings", text);
        Assert.Contains("Analyze a mod package and install after confirmation.", text);
        Assert.Contains("List assets or inspect a selected asset field tree.", text);
        Assert.Contains("Search assets using manifest include rules.", text);
        Assert.DoesNotContain("Preview or apply direct assets field patches.", text);
        Assert.Contains("Adjust output detail for this session.", text);
        Assert.DoesNotContain("Exit", text);
        Assert.Contains("Shortcuts: ↑/↓ to choose | Esc to cancel | Ctrl + C to exit", text);
        Assert.DoesNotContain("Back", text);
    }

    [Fact]
    public void Run_WhenMainMenuWaitsForInput_HidesCursorUntilApplicationExit()
    {
        TestConsole inner = CreateConsole();
        SelectMainMenuOption(inner, MainMenuOption.Exit);
        var console = new RecordingCursorConsole(inner);
        var app = new TerminalApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        Assert.True(exitCode == 0, inner.Output);
        Assert.Contains(false, console.CursorStates);
        Assert.DoesNotContain(true, console.CursorStates.Take(console.CursorStates.Count - 1));
        Assert.True(console.CursorStates[^1]);
    }

    [Fact]
    public void Run_WhenSettingsPageWaitsForInput_DoesNotShowCursorBetweenNavigationPages()
    {
        TestConsole inner = CreateConsole();
        SelectMainMenuOption(inner, MainMenuOption.Settings);
        inner.Input.PushKey(ConsoleKey.Escape);
        SelectMainMenuOption(inner, MainMenuOption.Exit);
        var console = new RecordingCursorConsole(inner);
        var app = new TerminalApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        Assert.True(exitCode == 0, inner.Output);
        Assert.True(console.CursorStates.Count(state => !state) >= 2);
        Assert.DoesNotContain(true, console.CursorStates.Take(console.CursorStates.Count - 1));
        Assert.True(console.CursorStates[^1]);
    }

    [Fact]
    public void Run_WhenConsoleDoesNotSupportAnsi_UsesNumberedMainMenuFallback()
    {
        TestConsole console = CreateConsole(supportsAnsi: false);
        console.Input.PushKey(ConsoleKey.Escape);
        var app = new TerminalApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        string text = console.Output;
        Assert.True(exitCode == 0, console.Output);
        Assert.Equal(1, CountOccurrences(text, "Unity Assets Patcher"));
        Assert.DoesNotContain("| Unity Assets Patcher |", text);
        Assert.DoesNotContain("Inspect, find, patch, and install Unity assets file mods.", text);
        Assert.Contains("Unity Assets Patcher", text);
        Assert.Contains("1. Install Mod", text);
        Assert.DoesNotContain("Install mod package", text);
        Assert.DoesNotContain("Preview a mod install", text);
        Assert.DoesNotContain("Preview install", text);
        Assert.DoesNotContain("Apply install", text);
        Assert.Contains("4. Settings", text);
        Assert.Contains("Analyze a mod package and install after confirmation.", text);
        Assert.DoesNotContain("5. Exit", text);
    }

    [Fact]
    public void Run_WhenConsoleDoesNotSupportAnsi_SubMenuReceivesEscape_ReturnsToMainMenu()
    {
        TestConsole console = CreateConsole(supportsAnsi: false);
        console.Input.PushTextWithEnter("2");
        console.Input.PushKey(ConsoleKey.Escape);
        console.Input.PushKey(ConsoleKey.Escape);
        var app = new TerminalApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        string text = console.Output;
        Assert.True(exitCode == 0, console.Output);
        Assert.Contains("Inspect assets", text);
        Assert.DoesNotContain("Assets file path", text);
        Assert.DoesNotContain("Back", text);
    }

    [Fact]
    public void ReadExistingFilePath_WhenValueIsQ_TreatsInputAsAPath()
    {
        string assetsPath = CreateTempFile(".assets");
        TestConsole console = CreateConsole();
        console.Input.PushTextWithEnter("q");
        console.Input.PushTextWithEnter(assetsPath);
        var prompts = new InteractivePrompts(console);

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
    public void Run_WhenInspectListPageUsesDefaultLimit_PrintsAssetSummaryTable()
    {
        string assetsPath = CreateTempFile(".assets");
        var assets = Enumerable.Range(1, 105)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InspectAssets);
        SelectSubMenuOption(console, 0);
        console.Input.PushTextWithEnter(assetsPath);
        SelectSubMenuOption(console, 0);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(new StubAssetsFileService(assets), console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.DoesNotContain("| Unity Assets Patcher |", text);
            Assert.Contains("Inspect assets", text);
            Assert.Contains("Path ID", text);
            Assert.Contains("Asset100", text);
            Assert.DoesNotContain("Asset101", text);
            Assert.Contains("Showing 100 of 105 assets.", text);
            Assert.DoesNotContain("--all", text);
            Assert.DoesNotContain("--limit", text);
        }
        finally
        {
            File.Delete(assetsPath);
        }
    }

    [Fact]
    public void Run_WhenSubMenuReceivesEscape_ReturnsToMainMenu()
    {
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InspectAssets);
        console.Input.PushKey(ConsoleKey.Escape);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var service = new StubAssetsFileService([]);
        var app = new TerminalApp(service, console);

        int exitCode = app.Run();

        string text = console.Output;
        Assert.True(exitCode == 0, console.Output);
        Assert.Contains("Inspect assets", text);
        Assert.DoesNotContain("Assets file path", text);
        Assert.DoesNotContain("Back", text);
    }

    [Fact]
    public void Run_WhenInstallPromptReceivesEscape_ReturnsToMainMenu()
    {
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushKey(ConsoleKey.Escape);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(new StubAssetsFileService([]), console);

        int exitCode = app.Run();

        string text = console.Output;
        Assert.True(exitCode == 0, console.Output);
        Assert.Contains("Install Mod", text);
        Assert.Contains("Mod zip path", text);
        Assert.DoesNotContain("Game directory", text);
    }

    [Fact]
    public void Run_WhenInspectFieldsPageUsesPathId_PrintsSelectedAssetFieldTree()
    {
        string assetsPath = CreateTempFile(".assets");
        var reader = new StubAssetsFileService(
            [],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new(
                    "AudioClip",
                    "AudioClip",
                    null,
                    [new AssetsFieldInfo("m_Name", "string", "ambient", [])]),
            });
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InspectAssets);
        SelectSubMenuOption(console, 1);
        console.Input.PushTextWithEnter(assetsPath);
        console.Input.PushTextWithEnter("4");
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(reader, console);

        try
        {
            int exitCode = app.Run();

            Assert.True(exitCode == 0, console.Output);
            Assert.Equal(4, reader.ReceivedPathId);
            Assert.Contains("AudioClip (AudioClip)", console.Output);
            Assert.Contains("m_Name (string): ambient", console.Output);
        }
        finally
        {
            File.Delete(assetsPath);
        }
    }

    [Fact]
    public void Run_WhenFindPageUsesManifest_PrintsMatchingAssets()
    {
        string assetsPath = CreateTempFile(".assets");
        string configPath = CreateCameraPatchManifest(
            Path.GetFileName(assetsPath),
            """
            "include": [
              {
                "field of view": 90.0
              }
            ]
            """);
        var reader = new StubAssetsFileService(
            [
                new AssetsInfo(10, 20, "Camera", 128),
                new AssetsInfo(11, 20, "Camera", 128),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = CameraFieldTree("90.0"),
                [11] = CameraFieldTree("75.0"),
            });
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.FindAssets);
        console.Input.PushTextWithEnter(assetsPath);
        console.Input.PushTextWithEnter(configPath);
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(reader, console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Find assets", text);
            Assert.Contains("10", text);
            Assert.Contains("Camera", text);
            Assert.DoesNotContain(" 11 ", text);
        }
        finally
        {
            File.Delete(assetsPath);
            File.Delete(configPath);
        }
    }

    [Fact]
    public void Run_WhenInstallPageIsCanceled_PrintsDryRunSummaryWithoutInstalling()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        string targetPath = Path.Combine(gameDirectory, "Game_Data", "sharedassets0.assets");
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
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushTextWithEnter("n");
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(CreateCameraReader(), console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Install Mod", text);
            Assert.Contains("DRY RUN", text);
            Assert.Contains("Apply these changes?", text);
            Assert.Contains("Install canceled.", text);
            Assert.DoesNotContain("INSTALLED", text);
            Assert.Contains("Test Mod", text);
            Assert.Contains("1.0.0", text);
            Assert.Equal(1, CountOccurrences(text, "Version"));
            Assert.DoesNotContain("Test Mod 1.0.0", text);
            Assert.Contains("sharedassets0.assets", text);
            Assert.Contains("Operations", text);
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
    public void Run_WhenInstallPageIsConfirmed_PreviewsAndInstallsMod()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
        string targetPath = Path.Combine(gameDirectory, "Game_Data", "sharedassets0.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
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
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushTextWithEnter("y");
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(CreateCameraReader(), backupDirectory, console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Install Mod", text);
            Assert.Contains("DRY RUN", text);
            Assert.Contains("Apply these changes?", text);
            Assert.Contains("Apply these changes? y/N", text);
            Assert.DoesNotContain("[y/N] [y/n]", text);
            Assert.Contains("INSTALLED", text);
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
    public void Run_WhenSettingsToggleVerboseLogging_InstallPreviewPrintsFieldDiff()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
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
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.Settings);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Escape);
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushTextWithEnter("n");
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(CreateCameraReader(), console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Settings", text);
            Assert.Contains("[x] Verbose Logging", text);
            Assert.Contains("Shortcuts: Up/Down choose | Space toggle | Esc return", text);
            Assert.DoesNotContain("Enter or Esc", text);
            Assert.DoesNotContain("Detailed install output", text);
            Assert.Contains(
                "Show detailed install preview logs, including per-asset field changes.",
                FirstLineContaining(text, "Verbose Logging"));
            Assert.Contains("field of view", text);
            Assert.Contains("90.0 -> 75.0", text);
            Assert.DoesNotContain("Read package", text);
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void Run_WhenSettingsToggleInstallTimingDetails_InstallPreviewPrintsStageTimings()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = CreateGameDirectory("sharedassets0.assets");
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
        TestConsole console = CreateConsole();
        SelectMainMenuOption(console, MainMenuOption.Settings);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Escape);
        SelectMainMenuOption(console, MainMenuOption.InstallMod);
        console.Input.PushTextWithEnter(zipPath);
        console.Input.PushTextWithEnter(gameDirectory);
        console.Input.PushTextWithEnter("n");
        ReturnToMainMenu(console);
        SelectMainMenuOption(console, MainMenuOption.Exit);
        var app = new TerminalApp(CreateCameraReader(), console);

        try
        {
            int exitCode = app.Run();

            string text = console.Output;
            Assert.True(exitCode == 0, console.Output);
            Assert.Contains("Settings", text);
            Assert.Contains("[x] Install timing details", text);
            Assert.Contains("Shortcuts: Up/Down choose | Space toggle | Esc return", text);
            Assert.DoesNotContain("Enter or Esc", text);
            Assert.Contains(
                "Show per-stage package, search, analysis, patch, and copy timings.",
                FirstLineContaining(text, "Install timing details"));
            Assert.Contains("Read package", text);
            Assert.Contains("Analyze changes", text);
            Assert.DoesNotContain("field of view", text);
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(gameDirectory, true);
        }
    }

    private enum MainMenuOption
    {
        InstallMod,
        InspectAssets,
        FindAssets,
        Settings,
        Exit,
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

    private static void AssertTitleIsInsideCompactPanel(string text)
    {
        string titleLine = text
            .ReplaceLineEndings("\n")
            .Split('\n')
            .First(line => line.Contains("Unity Assets Patcher", StringComparison.Ordinal));

        Assert.True(titleLine.TrimStart().StartsWith('│') || titleLine.TrimStart().StartsWith('|'), titleLine);
        Assert.True(titleLine.Length < 40, titleLine);
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

    private static StubAssetsFileService CreateCameraReader()
    {
        return new StubAssetsFileService(
            [new AssetsInfo(50, 20, "Camera", 128)],
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

    private static string CreateCameraPatchManifest(string assetsFileName, string body)
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "target": "{{assetsFileName}}",
                "type": "Camera",
                {{body.Trim()}}
              }
              """);

        return configPath;
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
            Cursor = new RecordingCursor(inner.Cursor, CursorStates);
        }

        public List<bool> CursorStates { get; } = [];

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

        public RecordingCursor(IAnsiConsoleCursor inner, List<bool> states)
        {
            _inner = inner;
            _states = states;
        }

        public void Show(bool show)
        {
            _states.Add(show);
            _inner.Show(show);
        }

        public void SetPosition(int column, int line)
        {
            _inner.SetPosition(column, line);
        }

        public void Move(CursorDirection direction, int steps)
        {
            _inner.Move(direction, steps);
        }
    }
}
