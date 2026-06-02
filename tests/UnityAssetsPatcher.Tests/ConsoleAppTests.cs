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

    private sealed class StubAssetsReader : IAssetsReader
    {
        private readonly IReadOnlyList<AssetsInfo> _result;

        public StubAssetsReader(IReadOnlyList<AssetsInfo> result)
        {
            _result = result;
        }

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            return _result;
        }
    }
}
