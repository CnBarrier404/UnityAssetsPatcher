using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace UnityAssetsPatcher.CLI;

public sealed class CLIApplication
{
    private readonly RootCommand _rootCommand;
    private readonly InvocationConfiguration _invocationConfiguration;
    private readonly TextWriter _error;
    private readonly CLIOptions? _options;

    public CLIApplication(
        IEnumerable<ICLICommand> commands,
        TextWriter output,
        TextWriter error,
        CLIOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        _rootCommand = new RootCommand("Inspect, install, and uninstall Unity assets file mods.");
        _error = error;
        _options = options;

        if (options is not null)
        {
            _rootCommand.Options.Add(options.Format);
        }

        foreach (ICLICommand command in commands)
        {
            _rootCommand.Subcommands.Add(command.Command);
        }

        _invocationConfiguration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
            EnableDefaultExceptionHandler = false
        };
    }

    public int Run(IReadOnlyList<string> arguments)
    {
        return RunAsync(arguments).GetAwaiter().GetResult();
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        ParseResult parseResult = _rootCommand.Parse(arguments);

        if (parseResult.Errors.Count == 0)
        {
            return await parseResult
                .InvokeAsync(_invocationConfiguration, cancellationToken)
                .ConfigureAwait(false);
        }

        if (parseResult.Action is ParseErrorAction parseErrorAction)
        {
            parseErrorAction.ShowHelp = true;
        }

        if (_options is not null && parseResult.GetValue(_options.Format) == CLIOutputFormat.Json)
        {
            CLIOutput.WriteUsageFailure(
                _error,
                GetCommandName(parseResult),
                parseResult.Errors.Select(error => error.Message));

            return CLIExitCodes.UsageError;
        }

        var errorConfiguration = new InvocationConfiguration
        {
            Output = _error,
            Error = _error,
            EnableDefaultExceptionHandler = false
        };

        await parseResult.InvokeAsync(errorConfiguration, cancellationToken).ConfigureAwait(false);

        return CLIExitCodes.UsageError;
    }

    private static string GetCommandName(ParseResult parseResult)
    {
        var names = new List<string>();

        for (CommandResult current = parseResult.CommandResult;
             !ReferenceEquals(current, parseResult.RootCommandResult);
             current = (CommandResult)current.Parent!)
        {
            names.Add(current.Command.Name);
        }

        names.Reverse();

        return names.Count == 0 ? "root" : string.Join('.', names);
    }
}
