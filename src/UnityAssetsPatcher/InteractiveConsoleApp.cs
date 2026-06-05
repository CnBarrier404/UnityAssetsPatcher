using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Cli;

namespace UnityAssetsPatcher;

public sealed class InteractiveConsoleApp
{
    private readonly CommandContext _context;
    private readonly InteractivePrompts _prompts;

    public InteractiveConsoleApp(CommandContext context, TextReader input)
    {
        _context = context;
        _prompts = new InteractivePrompts(input, context.Output);
    }

    public int Run()
    {
        while (true)
        {
            WriteMainMenu();

            string? choice = _prompts.ReadRawLine();

            if (choice is null)
            {
                return 0;
            }

            switch (choice.Trim().ToLowerInvariant())
            {
                case "1":
                    RunMenuAction(() => RunInstall(apply: true));
                    break;
                case "2":
                    RunMenuAction(() => RunInstall(apply: false));
                    break;
                case "3":
                    RunMenuAction(RunInspect);
                    break;
                case "4":
                case "q":
                case "quit":
                case "exit":
                    return 0;
                default:
                    _context.Output.WriteLine("Invalid option. Enter 1, 2, 3, or 4.");
                    break;
            }

            _context.Output.WriteLine();
        }
    }

    private void WriteMainMenu()
    {
        _context.Output.WriteLine("========================================");
        _context.Output.WriteLine("        Unity Assets Patcher");
        _context.Output.WriteLine("========================================");
        _context.Output.WriteLine();
        _context.Output.WriteLine("1. Install a mod");
        _context.Output.WriteLine("2. Preview a mod install");
        _context.Output.WriteLine("3. Inspect assets");
        _context.Output.WriteLine("4. Exit");
        _context.Output.WriteLine();
        _context.Output.Write("Select an option: ");
    }

    private void RunMenuAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _context.Error.WriteLine(exception.Message);
        }
    }

    private void RunInstall(bool apply)
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine(apply ? "Install a mod" : "Preview a mod install");
        _context.Output.WriteLine();

        string? zipFilePath = _prompts.ReadExistingFilePath("Mod zip path");

        if (zipFilePath is null)
        {
            return;
        }

        string? gameDirectory = _prompts.ReadExistingDirectoryPath("Game directory");

        if (gameDirectory is null)
        {
            return;
        }

        _context.Output.WriteLine();
        _context.Output.WriteLine("Analyzing mod...");
        _context.Output.WriteLine();

        _context.UseService(service =>
        {
            InstallPreviewResult preview =
                service.PreviewInstallMod(new InstallPreviewRequest(zipFilePath, gameDirectory));
            ConsoleOutputFormatter.WriteInstallPreview(_context.Output, preview);

            return 0;
        });

        if (!apply)
        {
            return;
        }

        _context.Output.WriteLine();

        if (!_prompts.Confirm("Apply these changes? [y/N]"))
        {
            _context.Output.WriteLine("Install canceled.");
            return;
        }

        _context.Output.WriteLine();
        _context.UseService(service =>
        {
            InstallModResult result = service.InstallMod(
                new InstallModRequest(zipFilePath, gameDirectory, _context.BackupDirectory));
            ConsoleOutputFormatter.WriteInstallResult(_context.Output, result);

            return 0;
        });
    }

    private void RunInspect()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Inspect assets");
        _context.Output.WriteLine("Use the command-line inspect command for detailed asset browsing.");
    }
}
