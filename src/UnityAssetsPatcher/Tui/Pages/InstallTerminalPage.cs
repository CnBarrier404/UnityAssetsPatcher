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

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        TerminalOutputFormatter.WriteInfo(Context.Console, "Analyzing mod...");
        TerminalOutputFormatter.WriteBlankLine(Context.Console);

        string? gameDirectory = null;
        InstallPreviewResult? preview = TryPreviewInstall(zipFilePath, gameDirectory);

        if (preview is null)
        {
            gameDirectory = _prompts.ReadExistingDirectoryPath("Game directory");

            if (gameDirectory is null)
            {
                return false;
            }

            preview = TryPreviewInstall(zipFilePath, gameDirectory);
        }

        if (preview is null)
        {
            return true;
        }

        TerminalOutputFormatter.WriteInstallPreview(Context.Console, preview, Context.Settings);

        TerminalOutputFormatter.WriteBlankLine(Context.Console);

        if (!_prompts.Confirm("Apply these changes?"))
        {
            TerminalOutputFormatter.WriteInfo(Context.Console, "Install canceled.");

            return true;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        Context.UseInstallWorkflow(workflow =>
        {
            InstallModResult result = workflow.Install(
                new InstallModRequest(zipFilePath, gameDirectory, Context.BackupDirectory));
            TerminalOutputFormatter.WriteInstallResult(Context.Console, result, Context.Settings);

            return 0;
        });

        return true;
    }

    private InstallPreviewResult? TryPreviewInstall(string zipFilePath, string? gameDirectory)
    {
        InstallPreviewResult? preview = null;

        try
        {
            Context.UseInstallWorkflow(workflow =>
            {
                preview = workflow.Preview(new InstallPreviewRequest(zipFilePath, gameDirectory));

                return 0;
            });
        }
        catch (DirectoryNotFoundException exception) when (gameDirectory is null)
        {
            TerminalOutputFormatter.WriteInfo(Context.Console, exception.Message);
            TerminalOutputFormatter.WriteBlankLine(Context.Console);
        }

        return preview;
    }
}
