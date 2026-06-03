using System.CommandLine;
using UnityAssetsPatcher.Cli;
using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class CommandCatalogTests
{
    /// <summary>
    /// 验证显式命令目录注册了当前支持的顶层命令和分组命令。
    /// </summary>
    [Fact]
    public void BuildRootCommand_RegistersSupportedCommands()
    {
        var service = new AssetsWorkflowService(new StubAssetsReader());
        var context = new CommandContext(service, new ConsoleOutputFormatter(), TextWriter.Null, TextWriter.Null);

        RootCommand root = new CommandCatalog().BuildRootCommand(context);

        Command inspect = Assert.Single(root.Subcommands, command => command.Name == "inspect");
        Assert.Contains(inspect.Subcommands, command => command.Name == "list");
        Assert.Contains(inspect.Subcommands, command => command.Name == "fields");
        Assert.Contains(root.Subcommands, command => command.Name == "find");
        Command patch = Assert.Single(root.Subcommands, command => command.Name == "patch");
        Assert.Contains(patch.Subcommands, command => command.Name == "preview");
    }

    private sealed class StubAssetsReader : IAssetsReader
    {
        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            return [];
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            throw new InvalidOperationException("Not used by this test.");
        }
    }
}
