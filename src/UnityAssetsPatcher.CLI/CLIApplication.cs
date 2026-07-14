using System.CommandLine;
using System.CommandLine.Invocation;

namespace UnityAssetsPatcher.CLI;

public sealed class CLIApplication
{
    private readonly RootCommand _rootCommand;
    private readonly InvocationConfiguration _invocationConfiguration;
    private readonly TextWriter _error;

    public CLIApplication(
        IEnumerable<ICLICommand> commands,
        TextWriter output,
        TextWriter error)
    {
        _rootCommand = new RootCommand("Install and uninstall Unity assets file mods.");
        _error = error;

        foreach (ICLICommand command in commands)
        {
            _rootCommand.Subcommands.Add(command.Command);
        }

        _invocationConfiguration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
        };
    }

    public int Run(IReadOnlyList<string> arguments)
    {
        ParseResult parseResult = _rootCommand.Parse(arguments);

        if (parseResult.Errors.Count <= 0)
        {
            return parseResult.Invoke(_invocationConfiguration);
        }

        if (parseResult.Action is ParseErrorAction parseErrorAction)
        {
            parseErrorAction.ShowHelp = true;
        }

        parseResult.Invoke(new InvocationConfiguration
        {
            Output = _error,
            Error = _error,
        });

        return 2;
    }
}
