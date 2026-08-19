using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Features.RepositoryManagement;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.CLI;

public sealed class RepositoryCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CLIOptions _options;

    public RepositoryCLICommand(IServiceScopeFactory scopeFactory, CLIOptions options)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory;
        _options = options;
        Command = new Command("repository", "Manage the backup repository.");
        Command.Subcommands.Add(CreateClearCommand());
    }

    private Command CreateClearCommand()
    {
        var yes = new Option<bool>("--yes", "-y")
        {
            Description = "Confirm permanent deletion of the unsupported backup repository."
        };
        var command = new Command(
            "clear",
            "Permanently clear an unsupported backup repository and initialize the current format.");
        command.Options.Add(yes);
        command.Validators.Add(result =>
        {
            if (result.GetResult(yes) is not { Implicit: false } optionResult ||
                !optionResult.GetValueOrDefault<bool>())
            {
                result.AddError("Required option '--yes' was not provided.");
            }
        });
        command.SetAction(ExecuteClear);

        return command;
    }

    private async Task<int> ExecuteClear(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            var result = await dispatcher
                .DispatchAsync<ClearUnsupportedRepositoryRequest, OperationResult<RepositoryClearResult>>(
                    new ClearUnsupportedRepositoryRequest(),
                    cancellationToken)
                .ConfigureAwait(false);

            return CLIOutput.WriteResult(
                parseResult,
                _options,
                "repository.clear",
                result,
                CLIOutput.RepositoryClear,
                CLIOutput.WriteRepositoryClearText);
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "repository.clear", exception);
        }
    }
}
