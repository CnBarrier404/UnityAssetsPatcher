using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.CLI;

public sealed class InspectCLICommand : ICLICommand
{
    private const int DefaultLimit = 100;

    public Command Command { get; }

    private readonly IWorkflowService _workflowService;
    private readonly CLIOptions _options;

    public InspectCLICommand(IWorkflowService workflowService, CLIOptions options)
    {
        _workflowService = workflowService;
        _options = options;

        Command = new Command("inspect", "Inspect assets file contents.");
        Command.Subcommands.Add(CreateListCommand());
        Command.Subcommands.Add(CreateFieldsCommand());
    }

    private Command CreateListCommand()
    {
        var assetsFile = AssetsFileArgument();
        var all = new Option<bool>("--all")
        {
            Description = "Print every asset summary row.",
        };
        var limit = new Option<int?>("--limit")
        {
            Description = "Maximum number of asset summary rows to print.",
        };
        var command = new Command("list", "Print asset summary rows.");
        command.Arguments.Add(assetsFile);
        command.Options.Add(all);
        command.Options.Add(limit);
        command.Validators.Add(result =>
        {
            bool printAll = result.GetValue(all);
            int? rowLimit = result.GetValue(limit);

            if (printAll && result.GetResult(limit) is { Implicit: false })
            {
                result.AddError("--all and --limit cannot be used together.");
            }
            else if (rowLimit <= 0)
            {
                result.AddError("--limit must be greater than 0.");
            }
        });
        command.SetAction(parseResult => ExecuteList(
            parseResult,
            parseResult.GetRequiredValue(assetsFile),
            parseResult.GetValue(all) ? null : parseResult.GetValue(limit) ?? DefaultLimit));

        return command;
    }

    private Command CreateFieldsCommand()
    {
        var assetsFile = AssetsFileArgument();
        var pathId = new Argument<long>("path-id")
        {
            Description = "Asset Path ID to inspect.",
        };
        var command = new Command("fields", "Print the field tree for one asset.");
        command.Arguments.Add(assetsFile);
        command.Arguments.Add(pathId);
        command.SetAction(parseResult => ExecuteFields(
            parseResult,
            parseResult.GetRequiredValue(assetsFile),
            parseResult.GetRequiredValue(pathId)));

        return command;
    }

    private int ExecuteList(ParseResult parseResult, string assetsFilePath, int? limit)
    {
        try
        {
            string fullPath = Path.GetFullPath(assetsFilePath);
            InspectListResult result = _workflowService.InspectList(new InspectListRequest(fullPath, limit));
            return CLIOutput.WriteSuccess(
                parseResult,
                _options,
                "inspect.list",
                CLIOutput.InspectList(fullPath, result),
                output => CLIOutput.WriteInspectListText(output, result));
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "inspect.list", exception);
        }
    }

    private int ExecuteFields(ParseResult parseResult, string assetsFilePath, long pathId)
    {
        try
        {
            string fullPath = Path.GetFullPath(assetsFilePath);
            AssetField result = _workflowService.InspectFields(new InspectFieldsRequest(fullPath, pathId));
            return CLIOutput.WriteSuccess(
                parseResult,
                _options,
                "inspect.fields",
                CLIOutput.InspectFields(fullPath, pathId, result),
                output => CLIOutput.WriteInspectFieldsText(output, result));
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "inspect.fields", exception);
        }
    }

    private static Argument<string> AssetsFileArgument() => new("assets-file")
    {
        Description = "Path to the Unity assets file.",
    };
}
