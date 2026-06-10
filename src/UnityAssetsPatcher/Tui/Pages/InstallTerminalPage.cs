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

    public override TerminalPageResult Run()
    {
        NewPage(Title, "Analyze the package first, then confirm before writing files.");

        string? zipFilePath = _prompts.ReadExistingFilePath("Mod zip path");

        if (zipFilePath is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        Context.Renderer.PrepareOutputArea();
        Context.Renderer.WriteInfo("Analyzing mod...");
        Context.Renderer.WriteBlankLine();

        string? gameDirectory = null;
        InstallPreviewResult? preview = TryPreviewInstall(zipFilePath, gameDirectory);

        if (preview is null)
        {
            gameDirectory = _prompts.ReadExistingDirectoryPath("Game directory");

            if (gameDirectory is null)
            {
                return TerminalPageResult.ReturnToMenu(false);
            }

            preview = TryPreviewInstall(zipFilePath, gameDirectory);
        }

        if (preview is null)
        {
            return TerminalPageResult.ReturnToMenu();
        }

        Context.Renderer.WriteInstallPreview(preview, Context.Settings);

        Context.Renderer.WriteBlankLine();
        Context.Renderer.ShowShortcutHint();

        if (!_prompts.Confirm("Apply these changes?"))
        {
            Context.Renderer.WriteInfo("Install canceled.");

            return TerminalPageResult.ReturnToMenu();
        }

        Context.Renderer.WriteBlankLine();
        Context.UseInstallWorkflow(workflow =>
        {
            InstallModResult result = workflow.Install(
                new InstallModRequest(zipFilePath, gameDirectory, Context.BackupDirectory));
            Context.Renderer.WriteInstallResult(result, Context.Settings);

            return 0;
        });

        return TerminalPageResult.ReturnToMenu();
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
            Context.Renderer.WriteInfo(exception.Message);
            Context.Renderer.WriteBlankLine();
        }

        return preview;
    }
}
