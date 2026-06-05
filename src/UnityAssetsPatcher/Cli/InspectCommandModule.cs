using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Cli;

public sealed class InspectCommandModule : ICommandModule
{
    private const int MaxShowAssetsLimit = 100;

    public Command Build(CommandContext context)
    {
        var inspectCommand = new Command("inspect", "Inspect assets file contents.")
        {
            BuildListCommand(context),
            BuildFieldsCommand(context)
        };

        return inspectCommand;
    }

    private static Command BuildListCommand(CommandContext context)
    {
        var assetsFileArgument = new Argument<string>("assets-file")
        {
            Description = "Path to the Unity assets file."
        };
        var allOption = new Option<bool>("--all")
        {
            Description = "Print every asset summary row."
        };
        var limitOption = new Option<int>("--limit")
        {
            Description = "Maximum number of asset summary rows to print."
        };

        var command = new Command("list", "Print asset summary rows.")
        {
            assetsFileArgument,
            allOption,
            limitOption
        };

        command.SetAction(parseResult =>
        {
            bool all = parseResult.GetValue(allOption);
            int limit = parseResult.GetValue(limitOption);
            bool hasLimit = parseResult.GetResult(limitOption) is not null;

            if (all && hasLimit)
            {
                context.Error.WriteLine("--all and --limit cannot be used together.");
                return 1;
            }

            if (hasLimit && limit <= 0)
            {
                context.Error.WriteLine("--limit must be greater than 0.");
                return 1;
            }

            int? effectiveLimit = all ? null : hasLimit ? limit : MaxShowAssetsLimit;
            string assetsFilePath = parseResult.GetRequiredValue(assetsFileArgument);
            var request = new InspectListRequest(assetsFilePath, effectiveLimit);

            return context.UseService(service =>
            {
                var assets = service.InspectList(request);
                ConsoleOutputFormatter.WriteAssetSummary(context.Output, assets, request.Limit);

                return 0;
            });
        });

        return command;
    }

    private static Command BuildFieldsCommand(CommandContext context)
    {
        var assetsFileArgument = new Argument<string>("assets-file")
        {
            Description = "Path to the Unity assets file."
        };
        var pathIdArgument = new Argument<long>("path-id")
        {
            Description = "Asset Path ID to inspect."
        };
        var command = new Command("fields", "Print the field tree for one asset.")
        {
            assetsFileArgument,
            pathIdArgument
        };

        command.SetAction(parseResult =>
        {
            string assetsFilePath = parseResult.GetRequiredValue(assetsFileArgument);
            long pathId = parseResult.GetRequiredValue(pathIdArgument);

            return context.UseService(service =>
            {
                AssetsFieldInfo fieldTree = service.InspectFields(new InspectFieldsRequest(assetsFilePath, pathId));
                ConsoleOutputFormatter.WriteAssetFields(context.Output, fieldTree);

                return 0;
            });
        });

        return command;
    }
}
