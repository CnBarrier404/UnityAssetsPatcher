using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.CLI;

public sealed class UninstallCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly IWorkflowService _workflowService;
    private readonly CLIOptions _options;

    public UninstallCLICommand(
        IWorkflowService workflowService,
        CLIOptions options)
    {
        _workflowService = workflowService;
        _options = options;

        Command = new Command("uninstall", "List, preview, or uninstall installed mods.");
        Command.Subcommands.Add(CreateListCommand());
        Command.Subcommands.Add(CreatePreviewCommand());
        Command.Subcommands.Add(CreateApplyCommand());
    }

    private Command CreateListCommand()
    {
        var command = new Command("list", "List installed mod records.");
        command.SetAction(ExecuteList);
        return command;
    }

    private Command CreatePreviewCommand()
    {
        var id = IdOption();
        var gameDirectory = GameDirectoryOption();
        var command = new Command("preview", "Analyze an uninstall without changing game files.");
        command.Options.Add(id);
        command.Options.Add(gameDirectory);
        command.SetAction(parseResult => ExecutePreview(
            parseResult,
            parseResult.GetRequiredValue(id),
            parseResult.GetValue(gameDirectory)));
        return command;
    }

    private Command CreateApplyCommand()
    {
        var id = IdOption();
        var gameDirectory = GameDirectoryOption();
        var yes = YesOption();
        var command = new Command("apply", "Uninstall a mod and restore its files.");
        command.Options.Add(id);
        command.Options.Add(gameDirectory);
        command.Options.Add(yes);
        RequireConfirmation(command, yes);
        command.SetAction(parseResult => ExecuteApply(
            parseResult,
            parseResult.GetRequiredValue(id),
            parseResult.GetValue(gameDirectory)));
        return command;
    }

    private int ExecuteList(ParseResult parseResult)
    {
        try
        {
            OperationResult<IReadOnlyList<InstallRecordSummary>> result = _workflowService.ListInstalledMods();
            return CLIOutput.WriteResult(
                parseResult,
                _options,
                "uninstall.list",
                result,
                CLIOutput.InstalledMods,
                CLIOutput.WriteInstalledModsText);
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "uninstall.list", exception);
        }
    }

    private int ExecutePreview(ParseResult parseResult, string installId, string? gameDirectory)
    {
        try
        {
            OperationResult<UninstallPreviewResult> result = _workflowService.PreviewUninstall(
                new UninstallPreviewRequest(installId, FullPathOrNull(gameDirectory)));
            return CLIOutput.WriteResult(
                parseResult,
                _options,
                "uninstall.preview",
                result,
                CLIOutput.UninstallPreview,
                CLIOutput.WriteUninstallPreviewText);
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "uninstall.preview", exception);
        }
    }

    private int ExecuteApply(ParseResult parseResult, string installId, string? gameDirectory)
    {
        try
        {
            OperationResult<UninstallModResult> result = _workflowService.Uninstall(
                new UninstallModRequest(installId, FullPathOrNull(gameDirectory)));
            return CLIOutput.WriteResult(
                parseResult,
                _options,
                "uninstall.apply",
                result,
                CLIOutput.UninstallResult,
                CLIOutput.WriteUninstallResultText);
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "uninstall.apply", exception);
        }
    }

    private static Option<string> IdOption() => new("--id")
    {
        Description = "Stable install record ID from 'uninstall list'.",
        Required = true,
    };

    private static Option<string?> GameDirectoryOption() => new("--game-directory", "-g")
    {
        Description = "Game installation directory; omit to resolve it from Steam.",
    };

    private static Option<bool> YesOption() => new("--yes", "-y")
    {
        Description = "Confirm the mutating operation.",
    };

    private static void RequireConfirmation(Command command, Option<bool> yes)
    {
        command.Validators.Add(result =>
        {
            if (result.GetResult(yes) is not { Implicit: false } optionResult ||
                !optionResult.GetValueOrDefault<bool>())
            {
                result.AddError("Required option '--yes' was not provided.");
            }
        });
    }

    private static string? FullPathOrNull(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    }
}
