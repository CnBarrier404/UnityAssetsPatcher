using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Tui.Pages;

internal sealed class InstallTerminalPage : TerminalPage
{
    public override string Title => "Install Mod";
    public override string Description => "Analyze a mod package and install after confirmation.";

    private readonly InteractivePrompts _prompts;

    public InstallTerminalPage(TerminalAppContext context, InteractivePrompts prompts) : base(context)
    {
        _prompts = prompts;
    }

    public override bool Run()
    {
        NewPage(Title, "Analyze the package first, then confirm before writing files.");

        string? zipFilePath = _prompts.ReadExistingFilePath("Mod zip path");

        if (zipFilePath is null)
        {
            return false;
        }

        string? gameDirectory = _prompts.ReadExistingDirectoryPath("Game directory");

        if (gameDirectory is null)
        {
            return false;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        TerminalOutputFormatter.WriteInfo(Context.Console, "Analyzing mod...");
        TerminalOutputFormatter.WriteBlankLine(Context.Console);

        Context.UseService(service =>
        {
            InstallPreviewResult preview =
                service.PreviewInstallMod(new InstallPreviewRequest(zipFilePath, gameDirectory));
            TerminalOutputFormatter.WriteInstallPreview(Context.Console, preview, Context.Settings);

            return 0;
        });

        TerminalOutputFormatter.WriteBlankLine(Context.Console);

        if (!_prompts.Confirm("Apply these changes?"))
        {
            TerminalOutputFormatter.WriteInfo(Context.Console, "Install canceled.");

            return true;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        Context.UseService(service =>
        {
            InstallModResult result = service.InstallMod(
                new InstallModRequest(zipFilePath, gameDirectory, Context.BackupDirectory));
            TerminalOutputFormatter.WriteInstallResult(Context.Console, result, Context.Settings);

            return 0;
        });

        return true;
    }
}
