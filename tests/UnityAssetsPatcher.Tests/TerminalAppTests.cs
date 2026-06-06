using Xunit;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Tui;

namespace UnityAssetsPatcher.Tests;

public sealed class TerminalAppTests
{
    [Fact]
    public void Run_WhenStarted_PrintsTerminalMenuWithAllPages()
    {
        var input = new StringReader(Lines("6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(new StubAssetsFileService([]), input, output, error);

        int exitCode = app.Run();

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("Unity Assets Patcher", text);
        Assert.Contains("1. Install a mod", text);
        Assert.Contains("2. Preview a mod install", text);
        Assert.Contains("3. Inspect assets", text);
        Assert.Contains("4. Find assets", text);
        Assert.Contains("5. Patch assets", text);
        Assert.Contains("6. Exit", text);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_WhenMainMenuReceivesHiddenExitAlias_PrintsInvalidOption()
    {
        var input = new StringReader(Lines("q", "6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(new StubAssetsFileService([]), input, output, error);

        int exitCode = app.Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Invalid option. Enter 1, 2, 3, 4, 5, or 6.", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void ReadExistingFilePath_WhenValueIsQ_TreatsInputAsAPath()
    {
        string assetsPath = CreateTempFile(".assets");
        var input = new StringReader(Lines("q", assetsPath));
        var output = new StringWriter();
        var prompts = new InteractivePrompts(input, output);

        try
        {
            string? path = prompts.ReadExistingFilePath("Assets file path");

            Assert.Equal(assetsPath, path);
            Assert.Contains("File not found: q", output.ToString());
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
        var input = new StringReader(Lines("3", "1", assetsPath, "1", "6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(new StubAssetsFileService(assets), input, output, error);

        try
        {
            int exitCode = app.Run();

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("Inspect assets", text);
            Assert.Contains("Path ID", text);
            Assert.Contains("Asset100", text);
            Assert.DoesNotContain("Asset101", text);
            Assert.Contains("Showing 100 of 105 assets.", text);
            Assert.DoesNotContain("--all", text);
            Assert.DoesNotContain("--limit", text);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(assetsPath);
        }
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
        var input = new StringReader(Lines("3", "2", assetsPath, "4", "6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(reader, input, output, error);

        try
        {
            int exitCode = app.Run();

            Assert.Equal(0, exitCode);
            Assert.Equal(4, reader.ReceivedPathId);
            Assert.Contains("AudioClip (AudioClip)", output.ToString());
            Assert.Contains("  m_Name (string): ambient", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
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
        var input = new StringReader(Lines("4", assetsPath, configPath, "6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(reader, input, output, error);

        try
        {
            int exitCode = app.Run();

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("Find assets", text);
            Assert.Contains("10", text);
            Assert.Contains("Camera", text);
            Assert.DoesNotContain("11", text);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(assetsPath);
            File.Delete(configPath);
        }
    }

    [Fact]
    public void Run_WhenPatchPreviewPageUsesManifest_PrintsPlannedChangesWithoutWriting()
    {
        string assetsPath = CreateTempFile(".assets");
        string configPath = CreateCameraPatchManifest(
            Path.GetFileName(assetsPath),
            """
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
            """);
        var reader = new StubAssetsFileService(
            [new AssetsInfo(30, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [30] = CameraFieldTree("90.0"),
            });
        var input = new StringReader(Lines("5", "1", assetsPath, configPath, "6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(reader, input, output, error);

        try
        {
            int exitCode = app.Run();

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("Patch assets", text);
            Assert.Contains("DRY RUN", text);
            Assert.Contains("Path ID: 30", text);
            Assert.Contains("field of view: 90.0 -> 75.0", text);
            Assert.Equal("original", File.ReadAllText(assetsPath));
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(assetsPath);
            File.Delete(configPath);
        }
    }

    [Fact]
    public void Run_WhenPatchApplyPageIsConfirmed_PrintsApplySummary()
    {
        string assetsPath = CreateTempFile(".assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string configPath = CreateCameraPatchManifest(
            Path.GetFileName(assetsPath),
            """
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
            """);
        var reader = new StubAssetsFileService(
            [new AssetsInfo(40, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [40] = CameraFieldTree("90.0"),
            });
        var input = new StringReader(Lines("5", "2", assetsPath, configPath, outputPath, "y", "6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(reader, input, output, error);

        try
        {
            int exitCode = app.Run();

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("DRY RUN", text);
            Assert.Contains("Apply these changes? [y/N]", text);
            Assert.Contains("APPLIED", text);
            Assert.Contains($"Output: {outputPath}", text);
            Assert.Equal("patched", File.ReadAllText(outputPath));
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(assetsPath);
            File.Delete(outputPath);
            File.Delete(configPath);
        }
    }

    [Fact]
    public void Run_WhenInstallPreviewPageUsesZipAndGameDir_PrintsDryRunSummary()
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
        var input = new StringReader(Lines("2", zipPath, gameDirectory, "6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(CreateCameraReader(), input, output, error);

        try
        {
            int exitCode = app.Run();

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("Preview a mod install", text);
            Assert.Contains("DRY RUN", text);
            Assert.Contains("Test Mod", text);
            Assert.Contains("field of view: 90.0 -> 75.0", text);
            Assert.Equal("original", File.ReadAllText(targetPath));
            Assert.Equal(string.Empty, error.ToString());
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
        var input = new StringReader(Lines("1", zipPath, gameDirectory, "y", "6"));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new TerminalApp(CreateCameraReader(), backupDirectory, input, output, error);

        try
        {
            int exitCode = app.Run();

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("Install a mod", text);
            Assert.Contains("DRY RUN", text);
            Assert.Contains("Apply these changes? [y/N]", text);
            Assert.Contains("INSTALLED", text);
            Assert.Equal("patched", File.ReadAllText(targetPath));
            Assert.Equal(string.Empty, error.ToString());
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

    private static string Lines(params string[] values)
    {
        return string.Join(Environment.NewLine, values) + Environment.NewLine;
    }
}
