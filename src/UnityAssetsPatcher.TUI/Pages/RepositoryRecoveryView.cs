using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class RepositoryRecoveryView : View
{
    internal RepositoryRecoveryView(
        LocalizedStrings strings,
        RepositoryRecoveryReport recovery,
        Func<string, Task> preview,
        Func<Task> retry,
        Action exit)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(exit);

        string details = recovery.Issues.Count == 0
            ? strings.RepositoryRecovery_InterruptedOperation
            : string.Join(Environment.NewLine, recovery.Issues.Select(issue =>
                OperationErrorFormatter.Format(strings, issue)));
        Add(new StyledLabel(strings.RepositoryRecovery_DamagedTitle, TextRole.Error)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        });
        Add(new StyledLabel(strings.RepositoryRecovery_DamagedDescription, TextRole.Preview)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill()
        });
        Add(new StyledLabel(details, TextRole.Error) { X = 0, Y = 4, Width = Dim.Fill() });

        var choices = new List<ChoiceItem>();

        if (recovery.Status == RepositoryRecoveryStatus.RecoveryRequired)
        {
            var input = new InputField { X = 0, Y = 7, Width = Dim.Fill() };
            var previewChoice = new ChoiceItem(
                    strings.RepositoryRecovery_PreviewAction,
                    strings.RepositoryRecovery_PreviewDescription)
                { X = 0, Y = 9 };
            previewChoice.Button.Accepted += async (_, _) =>
            {
                string path = TerminalPathNormalizer.Normalize(input.Text);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    await preview(path);
                }
            };
            input.Accepted += (_, _) => previewChoice.Button.SetFocus();
            Initialized += (_, _) => input.SetFocus();
            Add(new StyledLabel($"{strings.RepositoryRecovery_GameDirectoryPrompt}: ", TextRole.Label)
                { X = 0, Y = 6 }, input, previewChoice);
            choices.Add(previewChoice);
        }
        else
        {
            var retryChoice = new ChoiceItem(
                    strings.RepositoryRecovery_RetryAction,
                    strings.RepositoryRecovery_RetryDescription)
                { X = 0, Y = 7 };
            retryChoice.Button.Accepted += async (_, _) => await retry();
            Initialized += (_, _) => retryChoice.Button.SetFocus();
            Add(retryChoice);
            choices.Add(retryChoice);
        }

        var exitChoice = new ChoiceItem(
                strings.RepositoryRecovery_ExitAction,
                strings.RepositoryRecovery_ExitDescription)
            { X = 0, Y = 12 };
        exitChoice.Button.Accepted += (_, _) => exit();
        Add(exitChoice);
        choices.Add(exitChoice);
        ChoiceItem.AlignDescriptions(choices);
    }
}

public sealed class RepositoryRecoveryPreviewView : View
{
    internal RepositoryRecoveryPreviewView(
        LocalizedStrings strings,
        RepositoryRecoveryPreview preview,
        Func<Task> apply,
        Action back,
        Action exit)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(preview);
        var body = new ScrollableContentView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        string summary = $"{preview.Kind} {preview.InstallId} — {preview.Action}";
        body.Add(new StyledLabel(strings.RepositoryRecovery_PreviewTitle, TextRole.Preview)
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
                    strings.RepositoryRecovery_ApplyAction,
                    strings.RepositoryRecovery_ApplyDescription)
                { X = 0, Y = actionRow };
            applyChoice.Button.Accepted += async (_, _) => await apply();
            Initialized += (_, _) => applyChoice.Button.SetFocus();
            body.Add(applyChoice);
            choices.Add(applyChoice);
        }

        var backChoice = new ChoiceItem(
                strings.RepositoryRecovery_BackAction,
                strings.RepositoryRecovery_BackDescription)
            { X = 0, Y = actionRow + 2 };
        var exitChoice = new ChoiceItem(
                strings.RepositoryRecovery_ExitAction,
                strings.RepositoryRecovery_ExitDescription)
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
