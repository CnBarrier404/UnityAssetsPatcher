using System.Diagnostics;
using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Cli;

public sealed class InstallCommandModule : ICommandModule
{
    public Command Build(CommandContext context)
    {
        var zipFileArgument = new Argument<string>("zip-file")
        {
            Description = "Path to the mod zip file."
        };
        var gameDirectoryOption = new Option<string>("--game-dir")
        {
            Description = "Path to the game installation directory.",
            Required = true
        };
        var command = new Command("install", "Install a mod zip into a game directory.")
        {
            zipFileArgument,
            gameDirectoryOption,
            BuildPreviewCommand(context)
        };

        command.SetAction(parseResult =>
        {
            string zipFilePath = parseResult.GetRequiredValue(zipFileArgument);
            string gameDirectory = parseResult.GetRequiredValue(gameDirectoryOption);
            var stopwatch = Stopwatch.StartNew();

            return context.UseService(service =>
            {
                InstallModResult result = service.InstallMod(
                    new InstallModRequest(zipFilePath, gameDirectory, context.BackupDirectory));
                stopwatch.Stop();
                ConsoleOutputFormatter.WriteInstallResult(context.Output, result, stopwatch.Elapsed);

                return 0;
            });
        });

        return command;
    }

    private static Command BuildPreviewCommand(CommandContext context)
    {
        var zipFileArgument = new Argument<string>("zip-file")
        {
            Description = "Path to the mod zip file."
        };
        var gameDirectoryOption = new Option<string>("--game-dir")
        {
            Description = "Path to the game installation directory.",
            Required = true
        };
        var command = new Command("preview", "Print planned install changes without writing.")
        {
            zipFileArgument,
            gameDirectoryOption
        };

        command.SetAction(parseResult =>
        {
            string zipFilePath = parseResult.GetRequiredValue(zipFileArgument);
            string gameDirectory = parseResult.GetRequiredValue(gameDirectoryOption);
            var stopwatch = Stopwatch.StartNew();

            return context.UseService(service =>
            {
                InstallPreviewResult result = service.PreviewInstallMod(
                    new InstallPreviewRequest(zipFilePath, gameDirectory));
                stopwatch.Stop();
                ConsoleOutputFormatter.WriteInstallPreview(context.Output, result, stopwatch.Elapsed);

                return 0;
            });
        });

        return command;
    }
}
