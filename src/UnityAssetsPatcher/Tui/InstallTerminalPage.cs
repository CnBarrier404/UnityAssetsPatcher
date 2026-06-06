using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Tui;

internal sealed class InstallTerminalPage
{
    private readonly TerminalAppContext _context;
    private readonly InteractivePrompts _prompts;

    public InstallTerminalPage(TerminalAppContext context, InteractivePrompts prompts)
    {
        _context = context;
        _prompts = prompts;
    }

    public void Run(bool apply)
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
            TerminalOutputFormatter.WriteInstallPreview(_context.Output, preview);

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
            TerminalOutputFormatter.WriteInstallResult(_context.Output, result);

            return 0;
        });
    }
}
