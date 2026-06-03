using System.CommandLine;

namespace UnityAssetsPatcher.Cli;

public interface ICommandModule
{
    public Command Build(CommandContext context);
}
