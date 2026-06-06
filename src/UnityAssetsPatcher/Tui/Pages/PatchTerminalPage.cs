using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Tui.Pages;

internal sealed class PatchTerminalPage : TerminalPage
{
    public override string Title => "Patch assets";
    public override string Description => "Preview or apply direct assets field patches.";

    private readonly InteractivePrompts _prompts;
    private static readonly string[] PatchMenuChoices = [PreviewPatch, ApplyPatch,];

    private const string PreviewPatch = "Preview patch";
    private const string ApplyPatch = "Apply patch";
    private const string Cancel = "__cancel";

    public PatchTerminalPage(TerminalAppContext context, InteractivePrompts prompts) : base(context)
    {
        _prompts = prompts;
    }

    public override bool Run()
    {
        while (true)
        {
            NewPage(Title, "Preview patch operations before applying them to an assets file.");

            string choice = _prompts.ReadSubMenuChoice(string.Empty, PatchMenuChoices, Cancel);

            switch (choice)
            {
                case PreviewPatch:
                    return RunPreview();
                case ApplyPatch:
                    return RunApply();
                case Cancel:
                    return false;
            }
        }
    }

    private bool RunPreview()
    {
        NewPage("Preview patch", "Analyze planned field changes without writing files.");

        if (!TryReadPatchInputs(out string assetsFilePath, out string configPath))
        {
            return false;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        WritePreview(assetsFilePath, configPath);

        return true;
    }

    private bool RunApply()
    {
        NewPage("Apply patch", "Preview field changes, then confirm before writing files.");

        if (!TryReadPatchInputs(out string assetsFilePath, out string configPath) ||
            !_prompts.TryReadOptionalPath("Output path (blank to overwrite input)", out string? outputPath))
        {
            return false;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        WritePreview(assetsFilePath, configPath);

        TerminalOutputFormatter.WriteBlankLine(Context.Console);

        if (!_prompts.Confirm("Apply these changes?"))
        {
            TerminalOutputFormatter.WriteInfo(Context.Console, "Patch canceled.");

            return true;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        Context.UseService(service =>
        {
            PatchApplyResult result = service.ApplyPatch(
                new PatchApplyRequest(assetsFilePath, configPath, outputPath, Context.BackupDirectory));
            TerminalOutputFormatter.WritePatchApply(Context.Console, result);

            return 0;
        });

        return true;
    }

    private bool TryReadPatchInputs(out string assetsFilePath, out string configPath)
    {
        string? assetsPath = _prompts.ReadExistingFilePath("Assets file path");

        if (assetsPath is null)
        {
            assetsFilePath = string.Empty;
            configPath = string.Empty;
            return false;
        }

        string? manifestPath = _prompts.ReadExistingFilePath("Manifest JSON or mod zip path");

        if (manifestPath is null)
        {
            assetsFilePath = string.Empty;
            configPath = string.Empty;
            return false;
        }

        assetsFilePath = assetsPath;
        configPath = manifestPath;

        return true;
    }

    private void WritePreview(string assetsFilePath, string configPath)
    {
        Context.UseService(service =>
        {
            PatchPreviewResult preview = service.PreviewPatch(new PatchPreviewRequest(assetsFilePath, configPath));
            TerminalOutputFormatter.WritePatchPreview(Context.Console, preview);

            return 0;
        });
    }
}
