using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.CLI;

public sealed class InstallCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly IWorkflowService _workflowService;
    private readonly CLIOptions _options;

    public InstallCLICommand(
        IWorkflowService workflowService,
        CLIOptions options)
    {
        _workflowService = workflowService;
        _options = options;

        Command = new Command("install", "Preview or install a mod package.");
        Command.Subcommands.Add(CreatePreviewCommand());
        Command.Subcommands.Add(CreateApplyCommand());
    }

    private Command CreatePreviewCommand()
    {
        var package = PackageOption();
        var gameDirectory = GameDirectoryOption();
        var optionalGroups = OptionalGroupsOption();
        var command = new Command("preview", "Analyze an installation without changing files.");
        command.Options.Add(package);
        command.Options.Add(gameDirectory);
        command.Options.Add(optionalGroups);
        command.SetAction(parseResult => ExecutePreview(
            parseResult,
            parseResult.GetRequiredValue(package),
            parseResult.GetValue(gameDirectory),
            parseResult.GetValue(optionalGroups) ?? []));
        return command;
    }

    private Command CreateApplyCommand()
    {
        var package = PackageOption();
        var gameDirectory = GameDirectoryOption();
        var optionalGroups = OptionalGroupsOption();
        var yes = YesOption();
        var command = new Command("apply", "Install a mod package after validation.");
        command.Options.Add(package);
        command.Options.Add(gameDirectory);
        command.Options.Add(optionalGroups);
        command.Options.Add(yes);
        RequireConfirmation(command, yes);
        command.SetAction(parseResult => ExecuteApply(
            parseResult,
            parseResult.GetRequiredValue(package),
            parseResult.GetValue(gameDirectory),
            parseResult.GetValue(optionalGroups) ?? []));
        return command;
    }

    private int ExecutePreview(
        ParseResult parseResult,
        string packagePath,
        string? gameDirectory,
        IReadOnlyList<string> optionalGroups)
    {
        try
        {
            InstallPreviewResult result = _workflowService.PreviewInstall(CreateRequest(
                packagePath, gameDirectory, optionalGroups));
            return CLIOutput.WriteSuccess(
                parseResult,
                _options,
                "install.preview",
                CLIOutput.InstallPreview(result),
                output => CLIOutput.WriteInstallPreviewText(output, result));
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "install.preview", exception);
        }
    }

    private int ExecuteApply(
        ParseResult parseResult,
        string packagePath,
        string? gameDirectory,
        IReadOnlyList<string> optionalGroups)
    {
        try
        {
            InstallModResult result = _workflowService.Install(CreateRequest(
                packagePath, gameDirectory, optionalGroups));
            return CLIOutput.WriteSuccess(
                parseResult,
                _options,
                "install.apply",
                CLIOutput.InstallResult(result),
                output => CLIOutput.WriteInstallResultText(output, result));
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "install.apply", exception);
        }
    }

    private static InstallRequest CreateRequest(
        string packagePath,
        string? gameDirectory,
        IReadOnlyList<string> optionalGroups)
    {
        return new InstallRequest(Path.GetFullPath(packagePath), FullPathOrNull(gameDirectory))
        {
            SelectedOptionalGroups = optionalGroups,
        };
    }

    private static Option<string> PackageOption() => new("--package", "-p")
    {
        Description = "Mod ZIP package path.",
        Required = true,
    };

    private static Option<string?> GameDirectoryOption() => new("--game-directory", "-g")
    {
        Description = "Game installation directory; omit to resolve it from Steam.",
    };

    private static Option<string[]> OptionalGroupsOption() => new("--optional-group", "-o")
    {
        Description = "Optional content group to include; repeat for multiple groups.",
        Arity = ArgumentArity.OneOrMore,
        AllowMultipleArgumentsPerToken = false,
        DefaultValueFactory = _ => [],
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
