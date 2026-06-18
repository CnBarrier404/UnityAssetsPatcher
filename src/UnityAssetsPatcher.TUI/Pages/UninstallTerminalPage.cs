using System.Globalization;
using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class UninstallTerminalPage : TerminalPage
{
    public override string Title => LocalizedStrings.MainMenu_UninstallMod_Title;
    public override string Description => LocalizedStrings.MainMenu_UninstallMod_Description;

    public UninstallTerminalPage(TerminalAppContext context) : base(context) { }

    public override TerminalPageResult Run()
    {
        NewPage(Title, Description);

        var installed = Context.WorkflowService.ListInstalledMods();

        if (installed.Count == 0)
        {
            Context.Ui.Text.WriteInfo(LocalizedStrings.UninstallPage_NoInstalledModsFound);

            return TerminalPageResult.ReturnToMenu();
        }

        int? selectedIndex = Context.Prompts.ReadChoiceIndex(
            installed.Count,
            0,
            (index, clear) => WriteInstalledMods(installed, index, clear));

        if (selectedIndex is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        InstallRecordSummary selected = installed[selectedIndex.Value];
        Context.Ui.Layout.PrepareOutputArea();
        UninstallPreviewResult preview = Context.WorkflowService.PreviewUninstall(
            new UninstallPreviewRequest(selected.InstallDirectory));

        WritePreview(selected, preview);

        if (!preview.CanUninstall)
        {
            Context.Ui.Text.WriteBlankLine();
            Context.Ui.Text.WriteError(LocalizedStrings.UninstallPage_CannotUninstallMissingFiles);

            return TerminalPageResult.ReturnToMenu();
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Ui.Layout.ShowShortcutHint();

        if (!Context.Prompts.Confirm(LocalizedStrings.UninstallPage_ConfirmPrompt))
        {
            Context.Ui.Text.WriteInfo(LocalizedStrings.UninstallPage_UninstallCanceled);

            return TerminalPageResult.ReturnToMenu();
        }

        Context.Ui.Text.WriteBlankLine();
        UninstallModResult result = Context.WorkflowService.Uninstall(
            new UninstallModRequest(selected.InstallDirectory));
        WriteResult(result);

        return TerminalPageResult.ReturnToMenu();
    }

    private void WriteInstalledMods(
        IReadOnlyList<InstallRecordSummary> installed,
        int selectedIndex,
        bool clear)
    {
        Context.Ui.Layout.ShowPage(Title, Description, clear: clear);
        Context.Ui.List.WriteDescribedChoiceList(
            installed
                .Select(record => new TerminalChoiceDisplay(
                    $"{record.ModName} {record.ModVersion}",
                    $"{record.InstalledAt.LocalDateTime:g} | {record.GameDirectory}"))
                .ToArray(),
            selectedIndex);
    }

    private void WritePreview(InstallRecordSummary selected, UninstallPreviewResult preview)
    {
        Context.Ui.Status.Write(LocalizedStrings.UninstallPreview_Status, "yellow");
        Context.Ui.Summary.WriteRows(
            (LocalizedStrings.Summary_Mod, selected.ModName),
            (LocalizedStrings.Summary_Version, selected.ModVersion),
            (LocalizedStrings.UninstallSummary_GameDirectory, selected.GameDirectory),
            (LocalizedStrings.UninstallSummary_Installed,
                selected.InstalledAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)),
            (LocalizedStrings.UninstallSummary_RestoredFiles,
                preview.RestoredFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.UninstallSummary_PayloadFiles,
                preview.DeletedFiles.Count.ToString(CultureInfo.InvariantCulture)));

        if (preview.RestoredFiles.Count > 0)
        {
            Context.Ui.Text.WriteBlankLine();
            Context.Console.MarkupLine(
                $"[blue]{TerminalText.Escape(LocalizedStrings.UninstallPreview_FilesToRestore)}[/]");

            foreach (UninstallPreviewRestoredFileResult file in preview.RestoredFiles)
            {
                string status = file is { TargetExists: true, BackupExists: true }
                    ? LocalizedStrings.UninstallPreview_Ready
                    : LocalizedStrings.UninstallPreview_MissingRequiredFile;
                Context.Console.MarkupLine(
                    $"- {TerminalText.Escape(file.Target)}: {TerminalText.Escape(status)}");
            }
        }

        if (preview.DeletedFiles.Count <= 0)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine(
            $"[blue]{TerminalText.Escape(LocalizedStrings.UninstallPreview_PayloadFilesToDelete)}[/]");

        foreach (UninstallPreviewDeletedFileResult file in preview.DeletedFiles)
        {
            string status = file.Exists
                ? LocalizedStrings.UninstallPreview_WillDelete
                : LocalizedStrings.UninstallPreview_AlreadyMissing;
            Context.Console.MarkupLine(
                $"- {TerminalText.Escape(Path.GetFileName(file.DestinationPath))}: {TerminalText.Escape(status)}");
        }
    }

    private void WriteResult(UninstallModResult result)
    {
        Context.Ui.Status.Write(LocalizedStrings.UninstallResult_Status, "green");
        Context.Ui.Summary.WriteRows(
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.UninstallSummary_RestoredFiles,
                result.RestoredFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.UninstallSummary_DeletedFiles,
                result.DeletedFiles.Count(file => file.Deleted).ToString(CultureInfo.InvariantCulture)));

        if (result.RestoredFiles.Count > 0)
        {
            Context.Ui.Text.WriteBlankLine();
            Context.Console.MarkupLine(
                $"[blue]{TerminalText.Escape(LocalizedStrings.UninstallResult_RestoredFiles)}[/]");

            foreach (UninstallRestoredFileResult file in result.RestoredFiles)
            {
                Context.Console.MarkupLine(
                    $"- {TerminalText.Escape(file.Target)}: [grey]{TerminalText.Escape(file.AssetsFilePath)}[/]");
            }
        }

        if (result.DeletedFiles.Count <= 0)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Console.MarkupLine(
            $"[blue]{TerminalText.Escape(LocalizedStrings.UninstallResult_DeletedPayloadFiles)}[/]");

        foreach (UninstallDeletedFileResult file in result.DeletedFiles)
        {
            string status = file.Deleted
                ? LocalizedStrings.UninstallResult_Deleted
                : LocalizedStrings.UninstallPreview_AlreadyMissing;

            Context.Console.MarkupLine(
                $"- {TerminalText.Escape(Path.GetFileName(file.DestinationPath))}: {TerminalText.Escape(status)}");
        }
    }
}
