using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class BackupRecoveryView : View
{
    public BackupRecoveryView(
        BackupRecoveryReport recovery,
        Action<string> preview,
        Action retry,
        Action exit)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(exit);

        string details = recovery.Issues.Count == 0
            ? LocalizedStrings.BackupRecovery_InterruptedOperation
            : string.Join(Environment.NewLine, recovery.Issues.Select(issue => issue.Message));
        Add(new StyledLabel(LocalizedStrings.BackupRecovery_DamagedTitle, TextRole.Error)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
        });
        Add(new StyledLabel(LocalizedStrings.BackupRecovery_DamagedDescription, TextRole.Preview)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
        });
        Add(new StyledLabel(details, TextRole.Error) { X = 0, Y = 4, Width = Dim.Fill() });

        var choices = new List<ChoiceItem>();

        if (recovery.Status == BackupRepositoryStatus.RecoveryRequired)
        {
            var input = new InputField { X = 0, Y = 7, Width = Dim.Fill() };
            var previewChoice = new ChoiceItem(
                    LocalizedStrings.BackupRecovery_PreviewAction,
                    LocalizedStrings.BackupRecovery_PreviewDescription)
                { X = 0, Y = 9 };
            previewChoice.Button.Accepted += (_, _) =>
            {
                string path = TerminalPathNormalizer.Normalize(input.Text);
                if (!string.IsNullOrWhiteSpace(path)) preview(path);
            };
            input.Accepted += (_, _) => previewChoice.Button.SetFocus();
            Initialized += (_, _) => input.SetFocus();
            Add(new StyledLabel($"{LocalizedStrings.BackupRecovery_GameDirectoryPrompt}: ", TextRole.Label)
                { X = 0, Y = 6 }, input, previewChoice);
            choices.Add(previewChoice);
        }
        else
        {
            var retryChoice = new ChoiceItem(
                    LocalizedStrings.BackupRecovery_RetryAction,
                    LocalizedStrings.BackupRecovery_RetryDescription)
                { X = 0, Y = 7 };
            retryChoice.Button.Accepted += (_, _) => retry();
            Initialized += (_, _) => retryChoice.Button.SetFocus();
            Add(retryChoice);
            choices.Add(retryChoice);
        }

        var exitChoice = new ChoiceItem(
                LocalizedStrings.BackupRecovery_ExitAction,
                LocalizedStrings.BackupRecovery_ExitDescription)
            { X = 0, Y = 12 };
        exitChoice.Button.Accepted += (_, _) => exit();
        Add(exitChoice);
        choices.Add(exitChoice);
        ChoiceItem.AlignDescriptions(choices);
    }
}

public sealed class BackupRecoveryPreviewView : View
{
    public BackupRecoveryPreviewView(BackupRecoveryPreview preview, Action apply, Action back, Action exit)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var body = new ScrollableContentView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        string summary = $"{preview.Kind} {preview.InstallId} — {preview.Action}";
        body.Add(new StyledLabel(LocalizedStrings.BackupRecovery_PreviewTitle, TextRole.Preview)
            { X = 0, Y = 0, Width = Dim.Fill() });
        body.Add(new StyledLabel(preview.GameDirectory ?? string.Empty, TextRole.Label)
            { X = 0, Y = 2, Width = Dim.Fill() });
        body.Add(new StyledLabel(summary, TextRole.Preview) { X = 0, Y = 4, Width = Dim.Fill() });
        body.Add(new StyledLabel(string.Join(Environment.NewLine, preview.Files.Select(file =>
                $"- {file.Action}: {file.RelativePath}")), TextRole.Label)
            { X = 0, Y = 6, Width = Dim.Fill() });

        int actionRow = 8 + preview.Files.Count;
        var choices = new List<ChoiceItem>();
        if (preview.CanRecover)
        {
            var applyChoice = new ChoiceItem(
                    LocalizedStrings.BackupRecovery_ApplyAction,
                    LocalizedStrings.BackupRecovery_ApplyDescription)
                { X = 0, Y = actionRow };
            applyChoice.Button.Accepted += (_, _) => apply();
            Initialized += (_, _) => applyChoice.Button.SetFocus();
            body.Add(applyChoice);
            choices.Add(applyChoice);
        }

        var backChoice = new ChoiceItem(
                LocalizedStrings.BackupRecovery_BackAction,
                LocalizedStrings.BackupRecovery_BackDescription)
            { X = 0, Y = actionRow + 2 };
        var exitChoice = new ChoiceItem(
                LocalizedStrings.BackupRecovery_ExitAction,
                LocalizedStrings.BackupRecovery_ExitDescription)
            { X = 0, Y = actionRow + 4 };
        backChoice.Button.Accepted += (_, _) => back();
        exitChoice.Button.Accepted += (_, _) => exit();
        body.Add(backChoice, exitChoice);
        choices.Add(backChoice);
        choices.Add(exitChoice);
        ChoiceItem.AlignDescriptions(choices);
        body.SetContentHeightForRows(actionRow + 7);
        Add(body);
    }
}
