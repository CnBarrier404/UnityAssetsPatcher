using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.CLI;

public sealed class CheckCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<string> _getCurrentDirectory;
    private readonly TextWriter _error;
    private readonly Option<string?> _configOption;

    public CheckCLICommand(
        IServiceScopeFactory scopeFactory,
        Func<string> getCurrentDirectory,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(getCurrentDirectory);
        ArgumentNullException.ThrowIfNull(error);

        _scopeFactory = scopeFactory;
        _getCurrentDirectory = getCurrentDirectory;
        _error = error;
        _configOption = new Option<string?>("--config", "-c")
        {
            Description = "Manifest JSON or mod ZIP path (default: ./manifest.json)."
        };

        Command = new Command("check", "Validate a mod manifest.");

        Command.Options.Add(_configOption);

        Command.SetAction(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        string sourcePath = parseResult.GetValue(_configOption) ??
                            Path.Combine(_getCurrentDirectory(), "manifest.json");

        using IServiceScope scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var result = await dispatcher
            .DispatchAsync<CheckManifestRequest, OperationResult<CheckManifestResult>>(
                new CheckManifestRequest(sourcePath), cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            OperationSucceeded<CheckManifestResult> => CLIExitCodes.Success,
            OperationFailed<CheckManifestResult> failed => WriteFailure(failed.Error),
            _ => throw new InvalidOperationException("The check operation returned an unknown result.")
        };
    }

    private int WriteFailure(OperationError error)
    {
        CLIOutput.WriteFailure(_error, error);

        return CLIExitCodes.OperationFailed;
    }
}
