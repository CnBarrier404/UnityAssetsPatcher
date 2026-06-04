using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Cli;

public sealed class FindCommandModule : ICommandModule
{
    public Command Build(CommandContext context)
    {
        var assetsFileArgument = new Argument<string>("assets-file")
        {
            Description = "Path to the Unity assets file."
        };
        var configOption = new Option<string>("--config")
        {
            Description = "Path to the mod manifest JSON or zip.",
            Required = true
        };
        var command = new Command("find", "Find assets matching a mod manifest.")
        {
            assetsFileArgument,
            configOption
        };

        command.SetAction(parseResult =>
        {
            string assetsFilePath = parseResult.GetRequiredValue(assetsFileArgument);
            string configPath = parseResult.GetRequiredValue(configOption);
            var matches =
                context.Service.FindAssets(new FindAssetsRequest(assetsFilePath, configPath));
            ConsoleOutputFormatter.WriteFindResults(context.Output, matches);

            return 0;
        });

        return command;
    }
}
