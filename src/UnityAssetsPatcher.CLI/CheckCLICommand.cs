using System.CommandLine;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.CLI;

public sealed class CheckCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly CheckManifestWorkflow _workflow;
    private readonly Func<string> _getCurrentDirectory;
    private readonly TextWriter _error;
    private readonly Option<string?> _configOption;

    public CheckCLICommand(
        CheckManifestWorkflow workflow,
        Func<string> getCurrentDirectory,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(getCurrentDirectory);
        ArgumentNullException.ThrowIfNull(error);

        _workflow = workflow;
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
            var result = await _workflow
                .RunAsync(new CheckManifestRequest(sourcePath), cancellationToken)
                .ConfigureAwait(false);

            if (result is not OperationFailed<CheckManifestResult> failure)
            {
                return CLIExitCodes.Success;
            }

            CLITextOutput.WriteFailure(_error, failure.Error);

            return CLIExitCodes.OperationFailed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            CLITextOutput.WriteUnexpectedFailure(_error, exception);

            return CLIExitCodes.OperationFailed;
        }
    }
}
