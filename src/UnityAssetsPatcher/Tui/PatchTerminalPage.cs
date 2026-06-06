using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Tui;

internal sealed class PatchTerminalPage
{
    private readonly TerminalAppContext _context;
    private readonly InteractivePrompts _prompts;

    public PatchTerminalPage(TerminalAppContext context, InteractivePrompts prompts)
    {
        _context = context;
        _prompts = prompts;
    }

    public void Run()
    {
        while (true)
        {
            WriteMenu();

            string? choice = _prompts.ReadRawLine();

            if (choice is null)
            {
                return;
            }

            switch (choice.Trim().ToLowerInvariant())
            {
                case "1":
                    RunPreview();
                    return;
                case "2":
                    RunApply();
                    return;
                case "3":
                    return;
                default:
                    _context.Output.WriteLine("Invalid option. Enter 1, 2, or 3.");
                    break;
            }
        }
    }

    private void WriteMenu()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Patch assets");
        _context.Output.WriteLine();
        _context.Output.WriteLine("1. Preview patch");
        _context.Output.WriteLine("2. Apply patch");
        _context.Output.WriteLine("3. Back");
        _context.Output.WriteLine();
        _context.Output.Write("Select an option: ");
    }

    private void RunPreview()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Preview patch");
        _context.Output.WriteLine();

        if (!TryReadPatchInputs(out string assetsFilePath, out string configPath))
        {
            return;
        }

        _context.Output.WriteLine();
        WritePreview(assetsFilePath, configPath);
    }

    private void RunApply()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Apply patch");
        _context.Output.WriteLine();

        if (!TryReadPatchInputs(out string assetsFilePath, out string configPath) ||
            !_prompts.TryReadOptionalPath("Output path (blank to overwrite input)", out string? outputPath))
        {
            return;
        }

        _context.Output.WriteLine();
        WritePreview(assetsFilePath, configPath);

        _context.Output.WriteLine();

        if (!_prompts.Confirm("Apply these changes? [y/N]"))
        {
            _context.Output.WriteLine("Patch canceled.");

            return;
        }

        _context.Output.WriteLine();
        _context.UseService(service =>
        {
            PatchApplyResult result = service.ApplyPatch(
                new PatchApplyRequest(assetsFilePath, configPath, outputPath, _context.BackupDirectory));
            TerminalOutputFormatter.WritePatchApply(_context.Output, result);

            return 0;
        });
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
        _context.UseService(service =>
        {
            PatchPreviewResult preview = service.PreviewPatch(new PatchPreviewRequest(assetsFilePath, configPath));
            TerminalOutputFormatter.WritePatchPreview(_context.Output, preview);

            return 0;
        });
    }
}
