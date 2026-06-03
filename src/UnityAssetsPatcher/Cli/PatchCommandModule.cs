using System.CommandLine;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.Cli;

public sealed class PatchCommandModule : ICommandModule
{
    public Command Build(CommandContext context)
    {
        var patchCommand = new Command("patch", "Preview or apply assets file patches.") { BuildPreviewCommand(context) };

        return patchCommand;
    }

    private static Command BuildPreviewCommand(CommandContext context)
    {
        var assetsFileArgument = new Argument<string>("assets-file")
        {
            Description = "Path to the Unity assets file."
        };
        var configOption = new Option<string>("--config")
        {
            Description = "Path to the patch JSON config.",
            Required = true
        };
        var command = new Command("preview", "Print planned patch changes without writing.")
        {
            assetsFileArgument,
            configOption
        };

        command.SetAction(parseResult =>
        {
            string assetsFilePath = parseResult.GetRequiredValue(assetsFileArgument);
            string configPath = parseResult.GetRequiredValue(configOption);
            PatchPreviewResult preview =
                context.Service.PreviewPatch(new PatchPreviewRequest(assetsFilePath, configPath));
            ConsoleOutputFormatter.WritePatchPreview(context.Output, preview);

            return 0;
        });

        return command;
    }
}
