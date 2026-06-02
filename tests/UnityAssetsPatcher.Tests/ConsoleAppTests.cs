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

        int exitCode = app.Run(["inspect", "sharedassets0.assets"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Path ID", output.ToString());
        Assert.Contains("Camera", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// 验证缺少命令行参数时，程序会输出使用说明并返回非零退出码。
    /// </summary>
    [Fact]
    public void Run_WhenArgumentsAreMissing_PrintsUsageAndReturnsNonZeroExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new ConsoleApp(new StubAssetsReader([]), output, error);

        int exitCode = app.Run([]);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Usage:", error.ToString());
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

        int exitCode = app.Run(["inspect", "sharedassets0.assets", "4", "--detail"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(4, reader.ReceivedPathId);
        Assert.Contains("AudioClip (AudioClip)", output.ToString());
        Assert.Contains("  m_Name (string): ambient", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    private sealed class StubAssetsReader : IAssetsReader
    {
        private readonly IReadOnlyList<AssetsInfo> _result;
        private readonly AssetsFieldInfo? _fieldTree;

        public StubAssetsReader(IReadOnlyList<AssetsInfo> result, AssetsFieldInfo? fieldTree = null)
        {
            _result = result;
            _fieldTree = fieldTree;
        }

        public long? ReceivedPathId { get; private set; }

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            return _result;
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            ReceivedPathId = pathId;
            return _fieldTree ?? throw new InvalidOperationException("Field tree was not configured.");
        }
    }
}
