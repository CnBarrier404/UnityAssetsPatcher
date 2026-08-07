using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Failures;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.Messaging;

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
            Description = "Manifest JSON or mod ZIP path (default: ./manifest.json).",
        };

        Command = new Command("check", "Validate a mod manifest.");

        Command.Options.Add(_configOption);

        Command.SetAction(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        string sourcePath = parseResult.GetValue(_configOption) ??
                            Path.Combine(_getCurrentDirectory(), "manifest.json");

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            await dispatcher
                .DispatchAsync<CheckManifestRequest, CheckManifestResult>(
                    new CheckManifestRequest(sourcePath), cancellationToken)
                .ConfigureAwait(false);

            return CLIExitCodes.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApplicationFailureException exception)
        {
            CLIOutput.WriteFailure(_error, CLIErrorMapper.ToOperationError(exception));

            return CLIExitCodes.OperationFailed;
        }
        catch (Exception exception)
        {
            CLIOutput.WriteUnexpectedFailure(_error, exception);

            return CLIExitCodes.OperationFailed;
        }
    }
}
