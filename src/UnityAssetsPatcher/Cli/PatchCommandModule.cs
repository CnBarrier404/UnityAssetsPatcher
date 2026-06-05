using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Cli;

public sealed class PatchCommandModule : ICommandModule
{
    public Command Build(CommandContext context)
    {
        var patchCommand = new Command("patch", "Preview or apply assets file patches.")
        {
            BuildPreviewCommand(context),
            BuildApplyCommand(context)
        };

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
            Description = "Path to the mod manifest JSON or zip.",
            Required = true
        };
        var command = new Command("preview", "Print planned patch changes for an explicit assets file without writing.")
        {
            assetsFileArgument,
            configOption
        };

        command.SetAction(parseResult =>
        {
            string assetsFilePath = parseResult.GetRequiredValue(assetsFileArgument);
            string configPath = parseResult.GetRequiredValue(configOption);

            return context.UseService(service =>
            {
                PatchPreviewResult preview =
                    service.PreviewPatch(new PatchPreviewRequest(assetsFilePath, configPath));
                ConsoleOutputFormatter.WritePatchPreview(context.Output, preview);

                return 0;
            });
        });

        return command;
    }

    private static Command BuildApplyCommand(CommandContext context)
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
        var outputOption = new Option<string>("--output")
        {
            Description = "Path to write the patched assets file. Omit to overwrite the input file."
        };
        var command = new Command("apply", "Apply patch changes and write an assets file.")
        {
            assetsFileArgument,
            configOption,
            outputOption
        };

        command.SetAction(parseResult =>
        {
            string assetsFilePath = parseResult.GetRequiredValue(assetsFileArgument);
            string configPath = parseResult.GetRequiredValue(configOption);
            string? outputPath = parseResult.GetValue(outputOption);

            return context.UseService(service =>
            {
                PatchApplyResult result = service.ApplyPatch(
                    new PatchApplyRequest(assetsFilePath, configPath, outputPath, context.BackupDirectory));
                ConsoleOutputFormatter.WritePatchApply(context.Output, result);

                return 0;
            });
        });

        return command;
    }
}
