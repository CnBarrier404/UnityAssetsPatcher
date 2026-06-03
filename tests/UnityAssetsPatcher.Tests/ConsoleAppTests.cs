using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class ConsoleAppTests
{
    /// <summary>
    /// 验证 inspect 命令能够输出资产摘要表格并正常退出。
    /// </summary>
    [Fact]
    public void Run_WhenInspectCommandIsValid_PrintsAssetSummaryTable()
    {
        var reader = new StubAssetsReader(
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
    /// 验证 inspect 默认限制摘要输出数量，避免大型 assets 文件刷满终端。
    /// </summary>
    [Fact]
    public void Run_WhenInspectCommandHasManyAssets_PrintsLimitedSummaryAndTruncationHint()
    {
        var assets = Enumerable.Range(1, 201)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsReader(assets), output, error);

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
    /// 验证 inspect --all 会输出完整摘要表。
    /// </summary>
    [Fact]
    public void Run_WhenInspectCommandUsesAll_PrintsEveryAssetSummary()
    {
        var assets = Enumerable.Range(1, 201)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsReader(assets), output, error);

        int exitCode = app.Run(["inspect", "list", "resources.assets", "--all"]);

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("Asset201", text);
        Assert.DoesNotContain("Showing 200 of 201 assets.", text);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// 验证 inspect --limit 可以自定义摘要输出数量。
    /// </summary>
    [Fact]
    public void Run_WhenInspectCommandUsesLimit_PrintsRequestedAssetSummaryCount()
    {
        var assets = Enumerable.Range(1, 10)
            .Select(id => new AssetsInfo(id, 20, $"Asset{id}", 128))
            .ToArray();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsReader(assets), output, error);

        int exitCode = app.Run(["inspect", "list", "resources.assets", "--limit", "3"]);

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("Asset3", text);
        Assert.DoesNotContain("Asset4", text);
        Assert.Contains("Showing 3 of 10 assets.", text);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// 验证 inspect list 不允许同时使用 --all 和 --limit，避免产生歧义。
    /// </summary>
    [Fact]
    public void Run_WhenInspectListUsesAllAndLimit_PrintsErrorAndReturnsNonZeroExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsReader([]), output, error);

        int exitCode = app.Run(["inspect", "list", "resources.assets", "--all", "--limit", "3"]);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("--all", error.ToString());
        Assert.Contains("--limit", error.ToString());
    }

    /// <summary>
    /// 验证缺少命令行参数时，程序会输出解析错误并返回非零退出码。
    /// </summary>
    [Fact]
    public void Run_WhenArgumentsAreMissing_PrintsUsageAndReturnsNonZeroExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsReader([]), output, error);

        int exitCode = app.Run([]);

        Assert.NotEqual(0, exitCode);
        Assert.NotEqual(string.Empty, error.ToString());
    }

    /// <summary>
    /// 验证 inspect detail 模式会读取指定 Path ID，并按层级输出资产字段树。
    /// </summary>
    [Fact]
    public void Run_WhenInspectVerboseCommandIsValid_PrintsSelectedAssetFieldTree()
    {
        var reader = new StubAssetsReader(
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
    /// 验证 find --config 会按 JSON include 条件精准定位匹配资产。
    /// </summary>
    [Fact]
    public void Run_WhenFindCommandUsesConfig_PrintsOnlyAssetsMatchingAllIncludedFields()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
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
        var reader = new StubAssetsReader(
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
    /// 验证 include 数组中的多个对象按 OR 语义匹配。
    /// </summary>
    [Fact]
    public void Run_WhenFindConfigHasMultipleIncludeGroups_PrintsAssetsMatchingAnyGroup()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
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
        var reader = new StubAssetsReader(
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
    /// 验证 patch --dry-run 会按 include 定位资产，并输出 set 字段的改动预览。
    /// </summary>
    [Fact]
    public void Run_WhenPatchCommandUsesDryRun_PrintsPlannedChangesWithoutWriting()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
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
                  "path": "field of view",
                  "from": 90.0,
                  "to": 75.0
                }
              ]
            }
            """);
        var reader = new StubAssetsReader(
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
    /// 验证 patch --dry-run 在字段当前值不匹配 from 时会跳过该修改。
    /// </summary>
    [Fact]
    public void Run_WhenPatchDryRunSetFromDoesNotMatch_PrintsSkippedChange()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "type": "Camera",
              "include": [
                {
                  "field of view": 90.0
                }
              ],
              "set": [
                {
                  "path": "field of view",
                  "from": 60.0,
                  "to": 75.0
                }
              ]
            }
            """);
        var reader = new StubAssetsReader(
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

    private sealed class StubAssetsReader : IAssetsReader
    {
        private readonly IReadOnlyList<AssetsInfo> _result;
        private readonly IReadOnlyDictionary<long, AssetsFieldInfo> _fieldTrees;

        public StubAssetsReader(IReadOnlyList<AssetsInfo> result, AssetsFieldInfo? fieldTree = null)
            : this(result,
                fieldTree is null
                    ? new Dictionary<long, AssetsFieldInfo>()
                    : new Dictionary<long, AssetsFieldInfo> { [4] = fieldTree }) { }

        public StubAssetsReader(IReadOnlyList<AssetsInfo> result, IReadOnlyDictionary<long, AssetsFieldInfo> fieldTrees)
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
    }
}
