using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Recovery;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.CLI;

public sealed class RecoveryCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CLIOptions _options;

    public RecoveryCLICommand(IServiceScopeFactory scopeFactory, CLIOptions options)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory;
        _options = options;
        Command = new Command("recovery", "Preview or recover an interrupted install or uninstall.");
        Command.Subcommands.Add(CreatePreviewCommand());
        Command.Subcommands.Add(CreateApplyCommand());
    }

    private Command CreatePreviewCommand()
    {
        var gameDirectory = GameDirectoryOption();
        var command = new Command("preview", "Show every recovery action without changing files.");
        command.Options.Add(gameDirectory);
        command.SetAction((parseResult, cancellationToken) => ExecutePreview(
            parseResult,
            parseResult.GetRequiredValue(gameDirectory),
            cancellationToken));
        return command;
    }

    private Command CreateApplyCommand()
    {
        var gameDirectory = GameDirectoryOption();
        var yes = new Option<bool>("--yes", "-y") { Description = "Confirm the mutating operation." };
        var command = new Command("apply", "Recover an interrupted operation.");
        command.Options.Add(gameDirectory);
        command.Options.Add(yes);
        command.Validators.Add(result =>
        {
            if (result.GetResult(yes) is not { Implicit: false } optionResult ||
                !optionResult.GetValueOrDefault<bool>())
            {
                result.AddError("Required option '--yes' was not provided.");
            }
        });
        command.SetAction((parseResult, cancellationToken) => ExecuteApply(
            parseResult,
            parseResult.GetRequiredValue(gameDirectory),
            cancellationToken));
        return command;
    }

    private async Task<int> ExecutePreview(
        ParseResult parseResult,
        string gameDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            var result = await dispatcher
                .DispatchAsync<PreviewRecoveryRequest, OperationResult<RepositoryRecoveryPreview>>(
                    new PreviewRecoveryRequest(Path.GetFullPath(gameDirectory)),
                    cancellationToken)
                .ConfigureAwait(false);

            return CLIOutput.WriteResult(parseResult, _options, "recovery.preview", result,
                CLIOutput.RecoveryPreview, CLIOutput.WriteRecoveryPreviewText);
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "recovery.preview", exception);
        }
    }

    private async Task<int> ExecuteApply(
        ParseResult parseResult,
        string gameDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            var result = await dispatcher
                .DispatchAsync<RecoverRecoveryRequest, OperationResult<RepositoryRecoveryReport>>(
                    new RecoverRecoveryRequest(Path.GetFullPath(gameDirectory)),
                    cancellationToken)
                .ConfigureAwait(false);

            return CLIOutput.WriteResult(parseResult, _options, "recovery.apply", result,
                CLIOutput.RecoveryReport, CLIOutput.WriteRecoveryReportText);
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "recovery.apply", exception);
        }
    }

    private static Option<string> GameDirectoryOption()
    {
        return new Option<string>("--game-directory", "-g")
        {
            Description = "User-confirmed game installation directory.",
            Required = true
        };
    }
}
