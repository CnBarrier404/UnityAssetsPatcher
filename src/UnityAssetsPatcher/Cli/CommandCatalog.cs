using System.CommandLine;

namespace UnityAssetsPatcher.Cli;

public sealed class CommandCatalog
{
    private readonly IReadOnlyList<ICommandModule> _modules;

    public CommandCatalog() : this(
    [
        new InspectCommandModule(),
        new FindCommandModule(),
        new PatchCommandModule(),
    ]) { }

    private CommandCatalog(IReadOnlyList<ICommandModule> modules)
    {
        _modules = modules;
    }

    public RootCommand BuildRootCommand(CommandContext context)
    {
        var rootCommand =
            new RootCommand("A command-line tool for inspecting, querying, and modifying Unity assets files.");

        foreach (ICommandModule module in _modules)
        {
            rootCommand.Add(module.Build(context));
        }

        return rootCommand;
    }
}
