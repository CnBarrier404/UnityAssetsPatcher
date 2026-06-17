using System.Globalization;
using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Json;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class InstallTerminalPage : TerminalPage
{
    public override string Title => LocalizedStrings.MainMenu_InstallMod_Title;
    public override string Description => LocalizedStrings.MainMenu_InstallMod_Description;

    public InstallTerminalPage(TerminalAppContext context) : base(context) { }

    public override TerminalPageResult Run()
    {
        NewPage(Title, Description);

        string? zipFilePath = Context.Prompts.ReadExistingFilePath(LocalizedStrings.InstallPage_ModZipPathPrompt);

        if (zipFilePath is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        Context.Ui.Layout.PrepareOutputArea();
        Context.Ui.Text.WriteInfo(LocalizedStrings.InstallPage_AnalyzingMod);
        Context.Ui.Text.WriteBlankLine();

        string? gameDirectory = null;
        InstallPreviewResult? preview = TryPreviewInstall(zipFilePath, gameDirectory);

        if (preview is null)
        {
            gameDirectory = Context.Prompts.ReadExistingDirectoryPath(LocalizedStrings.InstallPage_GameDirectoryPrompt);

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

        if (!Context.Prompts.Confirm(LocalizedStrings.InstallPage_ApplyTheseChangesPrompt))
        {
            Context.Ui.Text.WriteInfo(LocalizedStrings.InstallPage_InstallCanceled);

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
        Context.Ui.Status.Write(LocalizedStrings.InstallPreview_DryRunStatus, "yellow");

        Context.Ui.Summary.WriteRows(
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.Summary_Author, result.ModAuthor),
            (LocalizedStrings.Summary_Elapsed, Context.Ui.Summary.FormatElapsedSecondsWithUnit(result.Timing.Elapsed)));

        WriteInstallPreviewTargets(result.Files);

        if (Context.Settings.VerboseLogging)
        {
            WriteInstallPreviewDetails(result.Files);
        }

        if (Context.Settings.VerboseLogging)
        {
            WriteInstallTiming(result.Timing);
        }
    }

    private void WriteInstallResult(InstallModResult result)
    {
        Context.Ui.Status.Write(LocalizedStrings.InstallResult_InstalledStatus, "green");
        Context.Ui.Summary.WriteRows(
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.InstallResult_PatchedFiles, result.Files.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.InstallResult_CopiedFiles,
                result.CopiedFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Assets,
                result.Files.Sum(file => file.AssetCount).ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Operations,
                result.Files.Sum(file => file.OperationCount).ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Elapsed, Context.Ui.Summary.FormatElapsedSecondsWithUnit(result.Timing.Elapsed)));

        WriteInstallResultTargets(result.Files);
        WriteInstallResultPayloads(result.CopiedFiles);

        if (Context.Settings.VerboseLogging)
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
        Context.Console.MarkupLine($"[blue]{TerminalText.Escape(LocalizedStrings.InstallPreview_Targets)}[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            Context.Console.MarkupLine(
                $"- {TerminalText.Escape(file.Target)}: [grey]{TerminalText.Escape(file.AssetsFilePath)}[/]");
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
            Context.Console.MarkupLine(
                $"[blue]Target[/] {TerminalText.Escape(file.Target)}");
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
        Context.Console.MarkupLine($"[blue]{TerminalText.Escape(LocalizedStrings.InstallResult_PatchedFiles)}[/]");

        foreach (InstallModFileResult file in files)
        {
            Context.Console.MarkupLine(
                $"- {TerminalText.Escape(file.Target)}: {FormatCount(file.AssetCount, LocalizedStrings.Summary_AssetUnit)}, {FormatCount(file.OperationCount, LocalizedStrings.Summary_OperationUnit)}");
            Context.Console.MarkupLine(
                $"  [grey]{TerminalText.Escape(LocalizedStrings.InstallResult_Backup)}[/] {TerminalText.Escape(file.BackupPath)}");
        }
    }

    private void WriteInstallResultPayloads(IReadOnlyList<InstallCopiedFileResult> copiedFiles)
    {
        if (copiedFiles.Count == 0)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine($"[blue]{TerminalText.Escape(LocalizedStrings.InstallResult_CopiedFiles)}[/]");

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
                    $"  {TerminalText.Escape(operation.Path)}: {TerminalText.Escape(operation.OldValue)} -> {TerminalText.Escape(JsonUtils.FormatElementValue(operation.To))}");
            }
        }
    }

    private string FormatCount(int count, string unit)
    {
        return Context.Ui.Summary.FormatCount(count, unit);
    }

    private void WriteInstallTiming(InstallTimingResult timing)
    {
        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine("[blue]Timing[/]");
        Context.Ui.Summary.WriteRows(
            ("Read package", Context.Ui.Summary.FormatElapsedSecondsWithUnit(timing.ReadPackage)),
            ("Prepare sources", Context.Ui.Summary.FormatElapsedSecondsWithUnit(timing.PrepareSources)),
            ("Find game files", Context.Ui.Summary.FormatElapsedSecondsWithUnit(timing.FindGameFiles)),
            ("Analyze changes", Context.Ui.Summary.FormatElapsedSecondsWithUnit(timing.AnalyzeChanges)),
            ("Apply patches", FormatTimingStage(timing.ApplyPatches)),
            ("Copy files", FormatTimingStage(timing.CopyFiles)));
    }

    private string FormatTimingStage(TimeSpan? elapsed)
    {
        return elapsed is null
            ? "skipped"
            : Context.Ui.Summary.FormatElapsedSecondsWithUnit(elapsed.Value);
    }
}
