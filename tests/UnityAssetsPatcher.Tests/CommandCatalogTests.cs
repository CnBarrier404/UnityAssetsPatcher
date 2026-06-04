using System.CommandLine;
using UnityAssetsPatcher.Cli;
using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class CommandCatalogTests
{
    /// <summary>
    /// Verifies that the explicit command catalog registers the currently supported top-level and grouped commands.
    /// </summary>
    [Fact]
    public void BuildRootCommand_RegistersSupportedCommands()
    {
        var service = new AssetsWorkflowService(new StubAssetsFileService());
        var context = new CommandContext(service, new ConsoleOutputFormatter(), "backup", TextWriter.Null,
            TextWriter.Null);

        RootCommand root = new CommandCatalog().BuildRootCommand(context);

        Command inspect = Assert.Single(root.Subcommands, command => command.Name == "inspect");
        Assert.Contains(inspect.Subcommands, command => command.Name == "list");
        Assert.Contains(inspect.Subcommands, command => command.Name == "fields");
        Command install = Assert.Single(root.Subcommands, command => command.Name == "install");
        Assert.Contains(install.Subcommands, command => command.Name == "preview");
        Assert.Contains(root.Subcommands, command => command.Name == "find");
        Command patch = Assert.Single(root.Subcommands, command => command.Name == "patch");
        Assert.Contains(patch.Subcommands, command => command.Name == "preview");
        Assert.Contains(patch.Subcommands, command => command.Name == "apply");
    }

    private sealed class StubAssetsFileService : IAssetsFileService
    {
        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            return [];
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            throw new InvalidOperationException("Not used by this test.");
        }

        public void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan)
        {
            throw new InvalidOperationException("Not used by this test.");
        }
    }
}
