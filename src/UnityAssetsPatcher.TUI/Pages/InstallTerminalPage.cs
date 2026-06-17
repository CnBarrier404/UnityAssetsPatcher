using System.Globalization;
using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Json;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class InstallTerminalPage : TerminalPage
{
    public override string Title => "Install Mod";
    public override string Description => "Analyze a mod package and install after confirmation.";

    public InstallTerminalPage(TerminalAppContext context) : base(context) { }

    public override TerminalPageResult Run()
    {
        NewPage(Title, "Analyze the package first, then confirm before writing files.");

        string? zipFilePath = Context.Prompts.ReadExistingFilePath("Mod zip path");

        if (zipFilePath is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        Context.Ui.Layout.PrepareOutputArea();
        Context.Ui.Text.WriteInfo("Analyzing mod...");
        Context.Ui.Text.WriteBlankLine();

        string? gameDirectory = null;
        InstallPreviewResult? preview = TryPreviewInstall(zipFilePath, gameDirectory);

        if (preview is null)
        {
            gameDirectory = Context.Prompts.ReadExistingDirectoryPath("Game directory");

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

        WriteInstallPreview(preview);

        Context.Ui.Text.WriteBlankLine();
        Context.Ui.Layout.ShowShortcutHint();

        if (!Context.Prompts.Confirm("Apply these changes?"))
        {
            Context.Ui.Text.WriteInfo("Install canceled.");

            return TerminalPageResult.ReturnToMenu();
        }

        Context.Ui.Text.WriteBlankLine();
        Context.UseInstallWorkflow(workflow =>
        {
            InstallModResult result = workflow.Install(
                new InstallModRequest(zipFilePath, gameDirectory, Context.BackupDirectory));
            WriteInstallResult(result);

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
            Context.Ui.Text.WriteInfo(exception.Message);
            Context.Ui.Text.WriteBlankLine();
        }

        return preview;
    }

    private void WriteInstallPreview(InstallPreviewResult result)
    {
        Context.Ui.Status.Write("DRY RUN", "yellow");

        int assetCount = result.Files.Sum(file => file.Preview.Assets.Count);
        int operationCount = CountPreviewOperations(result);
        int changingOperationCount = CountChangingPreviewOperations(result);

        Context.Ui.Summary.WriteRows(
            ("Mod", result.ModName),
            ("Version", result.ModVersion),
            ("Targets", result.Files.Count.ToString(CultureInfo.InvariantCulture)),
            ("Payload files", result.CopiedFiles.Count.ToString(CultureInfo.InvariantCulture)),
            ("Assets", assetCount.ToString(CultureInfo.InvariantCulture)),
            ("Operations", FormatOperationCounts(changingOperationCount, operationCount - changingOperationCount)),
            ("Elapsed", $"{Context.Ui.Summary.FormatElapsedSeconds(result.Timing.Elapsed)} s"));

        WriteInstallPreviewTargets(result.Files);
        WriteInstallPreviewPayloads(result.CopiedFiles);

        if (Context.Settings.VerboseLogging)
        {
            WriteInstallPreviewDetails(result.Files);
        }

        if (Context.Settings.InstallTimingDetails)
        {
            WriteInstallTiming(result.Timing);
        }
    }

    private void WriteInstallResult(InstallModResult result)
    {
        Context.Ui.Status.Write("INSTALLED", "green");
        Context.Ui.Summary.WriteRows(
            ("Mod", result.ModName),
            ("Version", result.ModVersion),
            ("Patched files", result.Files.Count.ToString(CultureInfo.InvariantCulture)),
            ("Copied files", result.CopiedFiles.Count.ToString(CultureInfo.InvariantCulture)),
            ("Assets", result.Files.Sum(file => file.AssetCount).ToString(CultureInfo.InvariantCulture)),
            ("Operations", result.Files.Sum(file => file.OperationCount).ToString(CultureInfo.InvariantCulture)),
            ("Elapsed", $"{Context.Ui.Summary.FormatElapsedSeconds(result.Timing.Elapsed)} s"));

        WriteInstallResultTargets(result.Files);
        WriteInstallResultPayloads(result.CopiedFiles);

        if (Context.Settings.InstallTimingDetails)
        {
            WriteInstallTiming(result.Timing);
        }
    }

    private void WriteInstallPreviewTargets(IReadOnlyList<InstallPreviewFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine("[blue]Targets[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            int assetCount = file.Preview.Assets.Count;
            int operationCount = file.Preview.Assets.Sum(asset => asset.Operations.Count);
            int changingOperationCount = file.Preview.Assets.Sum(asset =>
                asset.Operations.Count(operation => operation.WillChange));

            Context.Console.MarkupLine(
                $"- {TerminalText.Escape(file.Target)}: {FormatCount(assetCount, "asset")}, {FormatOperationCounts(changingOperationCount, operationCount - changingOperationCount)}");
            Context.Console.MarkupLine($"  [grey]{TerminalText.Escape(file.AssetsFilePath)}[/]");
        }
    }

    private void WriteInstallPreviewPayloads(IReadOnlyList<InstallCopyFilePreviewResult> copiedFiles)
    {
        if (copiedFiles.Count == 0)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine("[blue]Payload files[/]");

        foreach (InstallCopyFilePreviewResult copiedFile in copiedFiles)
        {
            string status = copiedFile.WillCopy ? "will copy" : "skipped, destination exists";
            Context.Console.MarkupLine(
                $"- {TerminalText.Escape(Path.GetFileName(copiedFile.Source))}: {TerminalText.Escape(status)}");
        }
    }

    private void WriteInstallPreviewDetails(IReadOnlyList<InstallPreviewFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine("[blue]Details[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            Context.Ui.Text.WriteBlankLine();
            Context.Console.MarkupLine($"[blue]Target[/] {TerminalText.Escape(file.Target)}");
            WritePatchPreviewAssets(file.Preview);
        }
    }

    private void WriteInstallResultTargets(IReadOnlyList<InstallModFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine("[blue]Patched files[/]");

        foreach (InstallModFileResult file in files)
        {
            Context.Console.MarkupLine(
                $"- {TerminalText.Escape(file.Target)}: {FormatCount(file.AssetCount, "asset")}, {FormatCount(file.OperationCount, "operation")}");
            Context.Console.MarkupLine($"  [grey]Backup[/] {TerminalText.Escape(file.BackupPath)}");
        }
    }

    private void WriteInstallResultPayloads(IReadOnlyList<InstallCopiedFileResult> copiedFiles)
    {
        if (copiedFiles.Count == 0)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine("[blue]Copied files[/]");

        foreach (InstallCopiedFileResult copiedFile in copiedFiles)
        {
            Context.Console.MarkupLine($"- {TerminalText.Escape(Path.GetFileName(copiedFile.DestinationPath))}");
        }
    }

    private void WritePatchPreviewAssets(PatchPreviewResult preview)
    {
        foreach (PatchPreviewAssetResult assetResult in preview.Assets)
        {
            Context.Ui.Text.WriteBlankLine();
            Context.Console.MarkupLine(
                $"[grey]Path ID {assetResult.Asset.PathId.ToString(CultureInfo.InvariantCulture)} ({TerminalText.Escape(assetResult.Asset.TypeName)})[/]");

            foreach (PatchPreviewOperationResult operation in assetResult.Operations)
            {
                if (!operation.WillChange)
                {
                    Context.Console.MarkupLine(
                        $"  {TerminalText.Escape(operation.Path)}: skipped, current value {TerminalText.Escape(operation.OldValue)} does not match expected {TerminalText.Escape(JsonUtils.FormatElementValue(operation.From))}");
                    continue;
                }

                Context.Console.MarkupLine(
                    $"  {TerminalText.Escape(operation.Path)}: {TerminalText.Escape(operation.OldValue)} [grey]->[/] {TerminalText.Escape(JsonUtils.FormatElementValue(operation.To))}");
            }
        }
    }

    private static int CountPreviewOperations(InstallPreviewResult result)
    {
        return result.Files.Sum(file => file.Preview.Assets.Sum(asset => asset.Operations.Count));
    }

    private static int CountChangingPreviewOperations(InstallPreviewResult result)
    {
        return result.Files.Sum(file => file.Preview.Assets.Sum(asset =>
            asset.Operations.Count(operation => operation.WillChange)));
    }

    private string FormatOperationCounts(int changingCount, int skippedCount)
    {
        string changing = FormatCount(changingCount, "operation");

        return skippedCount == 0
            ? changing
            : $"{changing} changing, {FormatCount(skippedCount, "operation")} skipped";
    }

    private string FormatCount(int count, string singular)
    {
        return Context.Ui.Summary.FormatCount(count, singular);
    }

    private void WriteInstallTiming(InstallTimingResult timing)
    {
        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine("[blue]Timing[/]");
        Context.Ui.Summary.WriteRows(
            ("Read package", $"{Context.Ui.Summary.FormatElapsedSeconds(timing.ReadPackage)} s"),
            ("Prepare sources", $"{Context.Ui.Summary.FormatElapsedSeconds(timing.PrepareSources)} s"),
            ("Find game files", $"{Context.Ui.Summary.FormatElapsedSeconds(timing.FindGameFiles)} s"),
            ("Analyze changes", $"{Context.Ui.Summary.FormatElapsedSeconds(timing.AnalyzeChanges)} s"),
            ("Apply patches", FormatTimingStage(timing.ApplyPatches)),
            ("Copy files", FormatTimingStage(timing.CopyFiles)));
    }

    private string FormatTimingStage(TimeSpan? elapsed)
    {
        return elapsed is null ? "skipped" : $"{Context.Ui.Summary.FormatElapsedSeconds(elapsed.Value)} s";
    }
}
