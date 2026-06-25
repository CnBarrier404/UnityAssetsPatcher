using System.Globalization;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class InstallTerminalView
{
    private readonly TerminalUI _ui;

    public InstallTerminalView(TerminalUI ui)
    {
        _ui = ui;
    }

    public void WriteAnalyzing()
    {
        _ui.Text.WriteInfo(LocalizedStrings.InstallPage_AnalyzingMod);
        _ui.Text.WriteBlankLine();
    }

    public void WriteInstallCanceled()
    {
        _ui.Text.WriteInfo(LocalizedStrings.InstallPage_InstallCanceled);
    }

    public void WriteInfo(string message)
    {
        _ui.Text.WriteInfo(message);
    }

    public void WriteBlankLine()
    {
        _ui.Text.WriteBlankLine();
    }

    public void WriteInstallPreview(InstallPreviewResult result, bool verboseLogging)
    {
        _ui.Status.WritePreview(LocalizedStrings.InstallPreview_DryRunStatus);

        _ui.Summary.WriteRows(
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.Summary_Author, result.ModAuthor),
            (LocalizedStrings.Summary_Elapsed, TerminalSummary.FormatElapsedSecondsWithUnit(result.Timing.Elapsed)));

        WriteInstallPreviewTargets(result.Files);

        if (verboseLogging)
        {
            WriteInstallPreviewDetails(result.Files);
            WriteInstallTiming(result.Timing);
        }
    }

    public void WriteInstallResult(InstallModResult result, bool verboseLogging)
    {
        _ui.Status.WriteSuccess(LocalizedStrings.InstallResult_InstalledStatus);
        _ui.Summary.WriteRows(
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.InstallResult_PatchedFiles, result.Files.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.InstallResult_CopiedFiles,
                result.CopiedFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Assets,
                result.Files.Sum(file => file.AssetCount).ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Operations,
                result.Files.Sum(file => file.OperationCount).ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Elapsed, TerminalSummary.FormatElapsedSecondsWithUnit(result.Timing.Elapsed)));

        WriteInstallResultTargets(result.Files);
        WriteInstallResultPayloads(result.CopiedFiles);
        WriteInstallResultOptionalGroups(result.OptionalGroups);

        if (verboseLogging)
        {
            WriteInstallTiming(result.Timing);
        }
    }

    private void WriteInstallResultOptionalGroups(IReadOnlyList<string> optionalGroups)
    {
        if (optionalGroups.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[{TerminalTheme.SectionHeader}]{TerminalText.Escape(LocalizedStrings.InstallResult_OptionalContent)}[/]");

        foreach (string group in optionalGroups)
        {
            _ui.Text.WriteMarkupLine($"- {TerminalText.Escape(group)}");
        }
    }

    private void WriteInstallPreviewTargets(IReadOnlyList<InstallPreviewFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[{TerminalTheme.SectionHeader}]{TerminalText.Escape(LocalizedStrings.InstallPreview_Targets)}[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            _ui.Text.WriteMarkupLine(
                $"- {TerminalText.Escape(file.Target)}: [{TerminalTheme.Muted}]{TerminalText.Escape(file.AssetsFilePath)}[/]");
        }
    }

    private void WriteInstallPreviewDetails(IReadOnlyList<InstallPreviewFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[{TerminalTheme.SectionHeader}]{TerminalText.Escape(LocalizedStrings.InstallPreview_Details)}[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            _ui.Text.WriteBlankLine();
            _ui.Text.WriteMarkupLine(
                $"[{TerminalTheme.SectionHeader}]{TerminalText.Escape(LocalizedStrings.InstallPreview_TargetLabel)}[/] {TerminalText.Escape(file.Target)}");
            WritePatchPreviewAssets(file.Preview);
        }
    }

    private void WriteInstallResultTargets(IReadOnlyList<InstallModFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[{TerminalTheme.SectionHeader}]{TerminalText.Escape(LocalizedStrings.InstallResult_PatchedFiles)}[/]");

        foreach (InstallModFileResult file in files)
        {
            _ui.Text.WriteMarkupLine(
                $"- {TerminalText.Escape(file.Target)}: {FormatCount(file.AssetCount, LocalizedStrings.Summary_AssetUnit)}, {FormatCount(file.OperationCount, LocalizedStrings.Summary_OperationUnit)}");
            _ui.Text.WriteMarkupLine(
                $"  [{TerminalTheme.Muted}]{TerminalText.Escape(LocalizedStrings.InstallResult_Backup)}[/] {TerminalText.Escape(file.BackupPath)}");
        }
    }

    private void WriteInstallResultPayloads(IReadOnlyList<InstallCopiedFileResult> copiedFiles)
    {
        if (copiedFiles.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[{TerminalTheme.SectionHeader}]{TerminalText.Escape(LocalizedStrings.InstallResult_CopiedFiles)}[/]");

        foreach (InstallCopiedFileResult copiedFile in copiedFiles)
        {
            _ui.Text.WriteMarkupLine($"- {TerminalText.Escape(Path.GetFileName(copiedFile.DestinationPath))}");
        }
    }

    private void WritePatchPreviewAssets(PatchPreviewResult preview)
    {
        foreach (PatchPreviewAssetResult assetResult in preview.Assets)
        {
            _ui.Text.WriteBlankLine();
            _ui.Text.WriteMarkupLine(
                $"[{TerminalTheme.Muted}]Path ID {assetResult.Asset.PathId.ToString(CultureInfo.InvariantCulture)} ({TerminalText.Escape(assetResult.Asset.TypeName)})[/]");

            foreach (PatchPreviewOperationResult operation in assetResult.Operations)
            {
                if (!operation.WillChange)
                {
                    _ui.Text.WriteMarkupLine(
                        $"  {TerminalText.Escape(operation.Path)}: skipped, current value {TerminalText.Escape(operation.OldValue)} does not match expected {TerminalText.Escape(operation.FromText)}");
                    continue;
                }

                _ui.Text.WriteMarkupLine(
                    $"  {TerminalText.Escape(operation.Path)}: {TerminalText.Escape(operation.OldValue)} -> {TerminalText.Escape(operation.ToText)}");
            }
        }
    }

    private static string FormatCount(int count, string unit)
    {
        return TerminalSummary.FormatCount(count, unit);
    }

    private void WriteInstallTiming(TimingSnapshot snapshot)
    {
        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[{TerminalTheme.SectionHeader}]{TerminalText.Escape(LocalizedStrings.Install_TimingHeader)}[/]");
        _ui.Summary.WriteRows(
            snapshot.Steps
                .Select(step => (FormatStepName(step.Name), TerminalSummary.FormatElapsedSecondsWithUnit(step.Elapsed)))
                .ToArray());
    }

    private static string FormatStepName(string step)
    {
        string[] words = step.Split('-', '_');
        words[0] = char.ToUpper(words[0][0]) + words[0][1..];
        return string.Join(' ', words);
    }
}
