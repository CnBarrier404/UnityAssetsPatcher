using System.Globalization;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class UninstallTerminalView
{
    private readonly TerminalUI _ui;

    public UninstallTerminalView(TerminalUI ui)
    {
        _ui = ui;
    }

    public void WriteNoInstalledModsFound()
    {
        _ui.Text.WriteInfo(LocalizedStrings.UninstallPage_NoInstalledModsFound);
    }

    public void WriteCannotUninstallMissingFiles()
    {
        _ui.Text.WriteBlankLine();
        _ui.Text.WriteError(LocalizedStrings.UninstallPage_CannotUninstallMissingFiles);
    }

    public void WriteUninstallCanceled()
    {
        _ui.Text.WriteInfo(LocalizedStrings.UninstallPage_UninstallCanceled);
    }

    public void WriteBlankLine()
    {
        _ui.Text.WriteBlankLine();
    }

    public void WriteInstalledMods(
        string title,
        string description,
        IReadOnlyList<InstallRecordSummary> installed,
        int selectedIndex,
        bool clear)
    {
        _ui.Layout.ShowPage(title, description, clear: clear);
        _ui.List.WriteDescribedChoiceList(
            installed
                .Select(record => new TerminalChoiceDisplay(
                    $"{record.ModName} {record.ModVersion}",
                    $"{record.InstalledAt.LocalDateTime:g} | {record.GameDirectory}"))
                .ToArray(),
            selectedIndex);
    }

    public void WritePreview(InstallRecordSummary selected, UninstallPreviewResult preview)
    {
        _ui.Status.Write(LocalizedStrings.UninstallPreview_Status, "yellow");
        _ui.Summary.WriteRows(
            (LocalizedStrings.Summary_Mod, selected.ModName),
            (LocalizedStrings.Summary_Version, selected.ModVersion),
            (LocalizedStrings.UninstallSummary_GameDirectory, selected.GameDirectory),
            (LocalizedStrings.UninstallSummary_Installed,
                selected.InstalledAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)),
            (LocalizedStrings.UninstallSummary_RestoredFiles,
                preview.RestoredFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.UninstallSummary_PayloadFiles,
                preview.DeletedFiles.Count.ToString(CultureInfo.InvariantCulture)));

        WriteRestoredPreviewFiles(preview.RestoredFiles);
        WriteDeletedPreviewFiles(preview.DeletedFiles);
    }

    public void WriteResult(UninstallModResult result)
    {
        _ui.Status.Write(LocalizedStrings.UninstallResult_Status, "green");
        _ui.Summary.WriteRows(
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.UninstallSummary_RestoredFiles,
                result.RestoredFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.UninstallSummary_DeletedFiles,
                result.DeletedFiles.Count(file => file.Deleted).ToString(CultureInfo.InvariantCulture)));

        WriteRestoredResultFiles(result.RestoredFiles);
        WriteDeletedResultFiles(result.DeletedFiles);
    }

    private void WriteRestoredPreviewFiles(IReadOnlyList<UninstallPreviewRestoredFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[blue]{TerminalText.Escape(LocalizedStrings.UninstallPreview_FilesToRestore)}[/]");

        foreach (UninstallPreviewRestoredFileResult file in files)
        {
            string status = file is { TargetExists: true, BackupExists: true }
                ? LocalizedStrings.UninstallPreview_Ready
                : LocalizedStrings.UninstallPreview_MissingRequiredFile;
            _ui.Text.WriteMarkupLine(
                $"- {TerminalText.Escape(file.Target)}: {TerminalText.Escape(status)}");
        }
    }

    private void WriteDeletedPreviewFiles(IReadOnlyList<UninstallPreviewDeletedFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[blue]{TerminalText.Escape(LocalizedStrings.UninstallPreview_PayloadFilesToDelete)}[/]");

        foreach (UninstallPreviewDeletedFileResult file in files)
        {
            string status = file.Exists
                ? LocalizedStrings.UninstallPreview_WillDelete
                : LocalizedStrings.UninstallPreview_AlreadyMissing;
            _ui.Text.WriteMarkupLine(
                $"- {TerminalText.Escape(Path.GetFileName(file.DestinationPath))}: {TerminalText.Escape(status)}");
        }
    }

    private void WriteRestoredResultFiles(IReadOnlyList<UninstallRestoredFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[blue]{TerminalText.Escape(LocalizedStrings.UninstallResult_RestoredFiles)}[/]");

        foreach (UninstallRestoredFileResult file in files)
        {
            _ui.Text.WriteMarkupLine(
                $"- {TerminalText.Escape(file.Target)}: [grey]{TerminalText.Escape(file.AssetsFilePath)}[/]");
        }
    }

    private void WriteDeletedResultFiles(IReadOnlyList<UninstallDeletedFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        _ui.Text.WriteBlankLine();
        _ui.Text.WriteMarkupLine(
            $"[blue]{TerminalText.Escape(LocalizedStrings.UninstallResult_DeletedPayloadFiles)}[/]");

        foreach (UninstallDeletedFileResult file in files)
        {
            string status = file.Deleted
                ? LocalizedStrings.UninstallResult_Deleted
                : LocalizedStrings.UninstallPreview_AlreadyMissing;

            _ui.Text.WriteMarkupLine(
                $"- {TerminalText.Escape(Path.GetFileName(file.DestinationPath))}: {TerminalText.Escape(status)}");
        }
    }
}
