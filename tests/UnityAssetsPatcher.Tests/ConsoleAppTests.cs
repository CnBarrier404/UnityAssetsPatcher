using System.IO.Compression;
using System.Text.RegularExpressions;
using Xunit;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tests;

public sealed class ConsoleAppTests
{
    /// <summary>
    /// Verifies that inspect can print an asset summary table and exit successfully.
    /// </summary>
    [Fact]
    public void Run_WhenInspectCommandIsValid_PrintsAssetSummaryTable()
    {
        var reader = new StubAssetsFileService(
        [
            new AssetsInfo(7, 20, "Camera", 128),
        ]);
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, output, error);

        int exitCode = app.Run(["inspect", "list", "sharedassets0.assets"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Path ID", output.ToString());
        Assert.Contains("Camera", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// Verifies that inspect limits summary output by default to avoid flooding the terminal for large assets files.
    /// </summary>
    [Fact]
    public void Run_WhenInspectCommandHasManyAssets_PrintsLimitedSummaryAndTruncationHint()
    {
        var assets = Enumerable.Range(1, 201)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsFileService(assets), output, error);

        int exitCode = app.Run(["inspect", "list", "resources.assets"]);

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("Asset100", text);
        Assert.DoesNotContain("Asset101", text);
        Assert.Contains("Showing 100 of 201 assets.", text);
        Assert.Contains("--all", text);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// Verifies that inspect --all prints the complete summary table.
    /// </summary>
    [Fact]
    public void Run_WhenInspectCommandUsesAll_PrintsEveryAssetSummary()
    {
        var assets = Enumerable.Range(1, 201)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsFileService(assets), output, error);

        int exitCode = app.Run(["inspect", "list", "resources.assets", "--all"]);

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("Asset201", text);
        Assert.DoesNotContain("Showing 200 of 201 assets.", text);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// Verifies that inspect --limit customizes the summary output count.
    /// </summary>
    [Fact]
    public void Run_WhenInspectCommandUsesLimit_PrintsRequestedAssetSummaryCount()
    {
        var assets = Enumerable.Range(1, 10)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsFileService(assets), output, error);

        int exitCode = app.Run(["inspect", "list", "resources.assets", "--limit", "3"]);

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("Asset3", text);
        Assert.DoesNotContain("Asset4", text);
        Assert.Contains("Showing 3 of 10 assets.", text);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// Verifies that inspect list rejects using --all and --limit together to avoid ambiguity.
    /// </summary>
    [Fact]
    public void Run_WhenInspectListUsesAllAndLimit_PrintsErrorAndReturnsNonZeroExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsFileService([]), output, error);

        int exitCode = app.Run(["inspect", "list", "resources.assets", "--all", "--limit", "3"]);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("--all", error.ToString());
        Assert.Contains("--limit", error.ToString());
    }

    /// <summary>
    /// Verifies that missing CLI arguments print a parse error and return a non-zero exit code.
    /// </summary>
    [Fact]
    public void Run_WhenArgumentsAreMissing_PrintsUsageAndReturnsNonZeroExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsFileService([]), output, error);

        int exitCode = app.Run([]);

        Assert.NotEqual(0, exitCode);
        Assert.NotEqual(string.Empty, error.ToString());
    }

    /// <summary>
    /// Verifies that inspect detail reads the specified Path ID and prints the asset field tree hierarchically.
    /// </summary>
    [Fact]
    public void Run_WhenInspectVerboseCommandIsValid_PrintsSelectedAssetFieldTree()
    {
        var reader = new StubAssetsFileService(
            [],
            new AssetsFieldInfo(
                "AudioClip",
                "AudioClip",
                null,
                [
                    new AssetsFieldInfo("m_Name", "string", "ambient", []),
                ]));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, output, error);

        int exitCode = app.Run(["inspect", "fields", "sharedassets0.assets", "4"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(4, reader.ReceivedPathId);
        Assert.Contains("AudioClip (AudioClip)", output.ToString());
        Assert.Contains("  m_Name (string): ambient", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// Verifies that find --config locates matching assets by JSON include conditions.
    /// </summary>
    [Fact]
    public void Run_WhenFindCommandUsesConfig_PrintsOnlyAssetsMatchingAllIncludedFields()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "resources.assets",
              "type": "Camera",
              "include": [
                {
                  "near clip plane": 0.01,
                  "far clip plane": 100,
                  "field of view": 90.0
                }
              ]
            }
            """);
        var reader = new StubAssetsFileService(
            [
                new AssetsInfo(10, 20, "Camera", 128),
                new AssetsInfo(11, 20, "Camera", 128),
                new AssetsInfo(12, 1, "GameObject", 128),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new(
                    "Camera",
                    "Camera",
                    null,
                    [
                        new AssetsFieldInfo("near clip plane", "float", "0.010000001", []),
                        new AssetsFieldInfo("far clip plane", "float", "100.0", []),
                        new AssetsFieldInfo("field of view", "float", "90.0", []),
                    ]),
                [11] = new(
                    "Camera",
                    "Camera",
                    null,
                    [
                        new AssetsFieldInfo("near clip plane", "float", "0.3", []),
                        new AssetsFieldInfo("far clip plane", "float", "100.0", []),
                        new AssetsFieldInfo("field of view", "float", "90.0", []),
                    ]),
            });
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, output, error);

        try
        {
            int exitCode = app.Run(["find", "resources.assets", "--config", configPath]);

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("10", text);
            Assert.Contains("Camera", text);
            Assert.DoesNotContain("11", text);
            Assert.DoesNotContain("12", text);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that multiple objects in the include array match with OR semantics.
    /// </summary>
    [Fact]
    public void Run_WhenFindConfigHasMultipleIncludeGroups_PrintsAssetsMatchingAnyGroup()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "resources.assets",
              "type": "Camera",
              "include": [
                {
                  "field of view": 60.0
                },
                {
                  "field of view": 90.0
                }
              ]
            }
            """);
        var reader = new StubAssetsFileService(
            [
                new AssetsInfo(20, 20, "Camera", 128),
                new AssetsInfo(21, 20, "Camera", 128),
                new AssetsInfo(22, 20, "Camera", 128),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [20] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "60.0", [])]),
                [21] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                [22] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "75.0", [])]),
            });
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, output, error);

        try
        {
            int exitCode = app.Run(["find", "resources.assets", "--config", configPath]);

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("20", text);
            Assert.Contains("21", text);
            Assert.DoesNotContain("22", text);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that patch --dry-run locates assets by include and prints previews for set field changes.
    /// </summary>
    [Fact]
    public void Run_WhenPatchCommandUsesDryRun_PrintsPlannedChangesWithoutWriting()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "resources.assets",
              "type": "Camera",
              "include": [
                {
                  "near clip plane": 0.01,
                  "far clip plane": 100,
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
            """);
        var reader = new StubAssetsFileService(
            [
                new AssetsInfo(30, 20, "Camera", 128),
                new AssetsInfo(31, 20, "Camera", 128),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [30] = new(
                    "Camera",
                    "Camera",
                    null,
                    [
                        new AssetsFieldInfo("near clip plane", "float", "0.01", []),
                        new AssetsFieldInfo("far clip plane", "float", "100.0", []),
                        new AssetsFieldInfo("field of view", "float", "90.0", []),
                    ]),
                [31] = new(
                    "Camera",
                    "Camera",
                    null,
                    [
                        new AssetsFieldInfo("near clip plane", "float", "0.3", []),
                        new AssetsFieldInfo("far clip plane", "float", "100.0", []),
                        new AssetsFieldInfo("field of view", "float", "90.0", []),
                    ]),
            });
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, output, error);

        try
        {
            int exitCode = app.Run(["patch", "preview", "resources.assets", "--config", configPath]);

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("DRY RUN", text);
            Assert.Contains("Path ID: 30", text);
            Assert.Contains("field of view: 90.0 -> 75.0", text);
            Assert.DoesNotContain("Path ID: 31", text);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that patch --dry-run skips a change when the current field value does not match from.
    /// </summary>
    [Fact]
    public void Run_WhenPatchDryRunSetFromDoesNotMatch_PrintsSkippedChange()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "resources.assets",
              "type": "Camera",
              "include": [
                {
                  "field of view": 90.0
                }
              ],
              "set": [
                {
                  "field": "field of view",
                  "from": 60.0,
                  "to": 75.0
                }
              ]
            }
            """);
        var reader = new StubAssetsFileService(
            [new AssetsInfo(40, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [40] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, output, error);

        try
        {
            int exitCode = app.Run(["patch", "preview", "resources.assets", "--config", configPath]);

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("Path ID: 40", text);
            Assert.Contains("skipped", text);
            Assert.Contains("current value 90.0 does not match expected 60.0", text);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that patch apply parses the output path and prints an apply summary.
    /// </summary>
    [Fact]
    public void Run_WhenPatchApplyCommandUsesOutput_PrintsApplySummary()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "target": "{{Path.GetFileName(inputPath)}}",
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
              """);
        var reader = new StubAssetsFileService(
            [new AssetsInfo(50, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [50] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, backupDirectory, output, error);

        try
        {
            int exitCode = app.Run(["patch", "apply", inputPath, "--config", configPath, "--output", outputPath]);

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("APPLIED", text);
            Assert.Contains(outputPath, text);
            Assert.Contains("Assets: 1", text);
            Assert.Contains("Operations: 1", text);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(configPath);
            File.Delete(inputPath);
            File.Delete(outputPath);
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    /// <summary>
    /// Verifies that install accepts a zip file and game install directory, then prints an install summary.
    /// </summary>
    [Fact]
    public void Run_WhenInstallCommandUsesZipAndGameDir_PrintsInstallSummary()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");
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
        var reader = new StubAssetsFileService(
            [new AssetsInfo(50, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [50] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, backupDirectory, output, error);

        try
        {
            int exitCode = app.Run(["install", zipPath, "--game-dir", gameDirectory]);

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("INSTALLED", text);
            Assert.Contains("Test Mod", text);
            Assert.Contains("Files: 1", text);
            Assert.Contains("Operations: 1", text);
            Assert.Matches(new Regex(@"^Elapsed: \d+(\.\d{1,3})? s\r?$", RegexOptions.Multiline), text);
            Assert.Contains("sharedassets0.assets", text);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(zipPath);
            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    /// <summary>
    /// Verifies that install preview locates target files from the zip manifest and prints dry-run results.
    /// </summary>
    [Fact]
    public void Run_WhenInstallPreviewUsesZipAndGameDir_PrintsDryRunSummary()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");
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
        var reader = new StubAssetsFileService(
            [new AssetsInfo(50, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [50] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, output, error);

        try
        {
            int exitCode = app.Run(["install", "preview", zipPath, "--game-dir", gameDirectory]);

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("DRY RUN", text);
            Assert.Contains("Test Mod", text);
            Assert.Contains("sharedassets0.assets", text);
            Assert.Contains("field of view: 90.0 -> 75.0", text);
            Assert.Matches(new Regex(@"^Elapsed: \d+(\.\d{1,3})? s\r?$", RegexOptions.Multiline), text);
            Assert.Equal("original", File.ReadAllText(targetPath));
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(zipPath);
            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    /// <summary>
    /// Verifies that install preview prints declared payload file copy plans.
    /// </summary>
    [Fact]
    public void Run_WhenInstallPreviewHasFiles_PrintsPayloadCopyPlan()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets4.assets");
        string copiedPath = Path.Combine(targetDirectory, "modassets.resource");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");

        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry manifestEntry = archive.CreateEntry("Mod/manifest.json");
            using (StreamWriter writer = new(manifestEntry.Open()))
            {
                writer.Write(
                    """
                    {
                      "name": "Test Mod",
                      "author": "UnityAssetsPatcher.Tests",
                      "version": "1.0.0",
                      "files": [
                        {
                          "source": "resources/modassets.resource"
                        }
                      ],
                      "patches": [
                        {
                          "target": "sharedassets4.assets",
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
            }

            ZipArchiveEntry payloadEntry = archive.CreateEntry("resources/modassets.resource");
            using StreamWriter payloadWriter = new(payloadEntry.Open());
            payloadWriter.Write("payload");
        }

        var reader = new StubAssetsFileService(
            [new AssetsInfo(50, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [50] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(reader, output, error);

        try
        {
            int exitCode = app.Run(["install", "preview", zipPath, "--game-dir", gameDirectory]);

            string text = output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("Copied files: 1", text);
            Assert.Contains($"resources/modassets.resource -> {copiedPath}", text);
            Assert.Equal("original", File.ReadAllText(targetPath));
            Assert.False(File.Exists(copiedPath));
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(zipPath);
            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    private sealed class StubAssetsFileService : IAssetsFileService
    {
        private readonly IReadOnlyList<AssetsInfo> _result;
        private readonly IReadOnlyDictionary<long, AssetsFieldInfo> _fieldTrees;

        public StubAssetsFileService(IReadOnlyList<AssetsInfo> result, AssetsFieldInfo? fieldTree = null)
            : this(result,
                fieldTree is null
                    ? new Dictionary<long, AssetsFieldInfo>()
                    : new Dictionary<long, AssetsFieldInfo> { [4] = fieldTree }) { }

        public StubAssetsFileService(IReadOnlyList<AssetsInfo> result,
            IReadOnlyDictionary<long, AssetsFieldInfo> fieldTrees)
        {
            _result = result;
            _fieldTrees = fieldTrees;
        }

        public long? ReceivedPathId { get; private set; }

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            return _result;
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            ReceivedPathId = pathId;
            return _fieldTrees.TryGetValue(pathId, out AssetsFieldInfo? fieldTree)
                ? fieldTree
                : throw new InvalidOperationException("Field tree was not configured.");
        }

        public void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan)
        {
            File.WriteAllText(outputPath, "patched");
        }

        public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
        {
            File.WriteAllText(outputPath, "patched");
        }
    }
}
