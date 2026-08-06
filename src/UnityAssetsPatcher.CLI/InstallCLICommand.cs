using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.CLI;

public sealed class InstallCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CLIOptions _options;

    public InstallCLICommand(
        IServiceScopeFactory scopeFactory,
        CLIOptions options)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory;
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
        command.SetAction((parseResult, cancellationToken) => ExecutePreview(
            parseResult,
            parseResult.GetRequiredValue(package),
            parseResult.GetValue(gameDirectory),
            parseResult.GetValue(optionalGroups) ?? [],
            cancellationToken));
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
        command.SetAction((parseResult, cancellationToken) => ExecuteApply(
            parseResult,
            parseResult.GetRequiredValue(package),
            parseResult.GetValue(gameDirectory),
            parseResult.GetValue(optionalGroups) ?? [],
            cancellationToken));
        return command;
    }

    private async Task<int> ExecutePreview(
        ParseResult parseResult,
        string packagePath,
        string? gameDirectory,
        IReadOnlyList<string> optionalGroups,
        CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            OperationResult<InstallPreviewResult> result = await dispatcher
                .DispatchAsync<PreviewInstallRequest, OperationResult<InstallPreviewResult>>(
                    new PreviewInstallRequest(CreateRequest(packagePath, gameDirectory, optionalGroups)),
                    cancellationToken)
                .ConfigureAwait(false);

            return CLIOutput.WriteResult(
                parseResult,
                _options,
                "install.preview",
                result,
                CLIOutput.InstallPreview,
                CLIOutput.WriteInstallPreviewText);
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "install.preview", exception);
        }
    }

    private async Task<int> ExecuteApply(
        ParseResult parseResult,
        string packagePath,
        string? gameDirectory,
        IReadOnlyList<string> optionalGroups,
        CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            OperationResult<InstallModResult> result = await dispatcher
                .DispatchAsync<InstallModRequest, OperationResult<InstallModResult>>(
                    new InstallModRequest(CreateRequest(packagePath, gameDirectory, optionalGroups)),
                    cancellationToken)
                .ConfigureAwait(false);

            return CLIOutput.WriteResult(
                parseResult,
                _options,
                "install.apply",
                result,
                CLIOutput.InstallResult,
                CLIOutput.WriteInstallResultText);
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
