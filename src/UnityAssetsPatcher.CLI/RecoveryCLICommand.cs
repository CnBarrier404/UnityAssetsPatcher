using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.CLI;

public sealed class RecoveryCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly IWorkflowService _workflowService;
    private readonly CLIOptions _options;

    public RecoveryCLICommand(IWorkflowService workflowService, CLIOptions options)
    {
        _workflowService = workflowService;
        _options = options;
        Command = new Command("recovery", "Preview or recover an interrupted install or uninstall.");
        Command.Subcommands.Add(CreatePreviewCommand());
        Command.Subcommands.Add(CreateApplyCommand());
    }

    private Command CreatePreviewCommand()
    {
        Option<string> gameDirectory = GameDirectoryOption();
        var command = new Command("preview", "Show every recovery action without changing files.");
        command.Options.Add(gameDirectory);
        command.SetAction(parseResult => ExecutePreview(parseResult, parseResult.GetRequiredValue(gameDirectory)));
        return command;
    }

    private Command CreateApplyCommand()
    {
        Option<string> gameDirectory = GameDirectoryOption();
        var yes = new Option<bool>("--yes", "-y") { Description = "Confirm the mutating operation." };
        var command = new Command("apply", "Recover an interrupted operation.");
        command.Options.Add(gameDirectory);
        command.Options.Add(yes);
        command.Validators.Add(result =>
        {
            if (result.GetResult(yes) is not { Implicit: false } optionResult ||
                !optionResult.GetValueOrDefault<bool>())
                result.AddError("Required option '--yes' was not provided.");
        });
        command.SetAction(parseResult => ExecuteApply(parseResult, parseResult.GetRequiredValue(gameDirectory)));
        return command;
    }

    private int ExecutePreview(ParseResult parseResult, string gameDirectory)
    {
        try
        {
            BackupRecoveryPreview preview = _workflowService.PreviewPendingTransaction(Path.GetFullPath(gameDirectory));
            return CLIOutput.WriteSuccess(parseResult, _options, "recovery.preview",
                CLIOutput.RecoveryPreview(preview), output => CLIOutput.WriteRecoveryPreviewText(output, preview));
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "recovery.preview", exception);
        }
    }

    private int ExecuteApply(ParseResult parseResult, string gameDirectory)
    {
        try
        {
            BackupRecoveryReport recovery =
                _workflowService.RecoverPendingTransactions(Path.GetFullPath(gameDirectory));
            if (recovery.Status == BackupRepositoryStatus.Locked)
                throw new BackupRecoveryException(recovery.Issues[0].Message, recovery);
            return CLIOutput.WriteSuccess(parseResult, _options, "recovery.apply",
                CLIOutput.RecoveryReport(recovery), output => CLIOutput.WriteRecoveryReportText(output, recovery));
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "recovery.apply", exception);
        }
    }

    private static Option<string> GameDirectoryOption() => new("--game-directory", "-g")
    {
        Description = "User-confirmed game installation directory.",
        Required = true,
    };
}
