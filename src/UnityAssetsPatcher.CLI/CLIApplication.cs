using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace UnityAssetsPatcher.CLI;

public sealed class CLIApplication
{
    private readonly RootCommand _rootCommand;
    private readonly InvocationConfiguration _invocationConfiguration;
    private readonly TextWriter _error;
    private readonly CLIOptions _options;

    public CLIApplication(
        IEnumerable<ICLICommand> commands,
        TextWriter output,
        TextWriter error,
        CLIOptions? options = null)
    {
        _options = options ?? new CLIOptions();
        _rootCommand = new RootCommand("Inspect, install, and uninstall Unity assets file mods.");
        _error = error;
        _rootCommand.Options.Add(_options.Format);

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

        if (parseResult.GetValue(_options.Format) == CLIOutputFormat.Json)
        {
            CLIOutput.WriteUsageFailure(
                _error,
                GetCommandName(parseResult),
                parseResult.Errors.Select(error => error.Message));
            return 2;
        }

        parseResult.Invoke(new InvocationConfiguration
        {
            Output = _error,
            Error = _error,
        });

        return 2;
    }

    private static string GetCommandName(ParseResult parseResult)
    {
        var names = new List<string>();

        for (var current = parseResult.CommandResult;
             !ReferenceEquals(current, parseResult.RootCommandResult);
             current = (CommandResult)current.Parent!)
        {
            names.Add(current.Command.Name);
        }

        names.Reverse();
        return names.Count == 0 ? "root" : string.Join('.', names);
    }
}
