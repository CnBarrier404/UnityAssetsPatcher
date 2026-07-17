using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class BackupRecoveryView : View
{
    public BackupRecoveryView(BackupRecoveryReport recovery, Action retry, Action exit)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(exit);

        string details = recovery.Issues.Count == 0
            ? LocalizedStrings.BackupRecovery_InterruptedOperation
            : string.Join(Environment.NewLine, recovery.Issues.Select(issue => issue.Message));
        var heading = new StyledLabel(LocalizedStrings.BackupRecovery_DamagedTitle, TextRole.Error)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
        };
        var message = new StyledLabel(LocalizedStrings.BackupRecovery_DamagedDescription, TextRole.Preview)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
        };
        var issue = new StyledLabel(details, TextRole.Error)
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
        };
        var retryChoice = new ChoiceItem(
            LocalizedStrings.BackupRecovery_RetryAction,
            LocalizedStrings.BackupRecovery_RetryDescription)
        {
            X = 0,
            Y = 7,
        };
        var exitChoice = new ChoiceItem(
            LocalizedStrings.BackupRecovery_ExitAction,
            LocalizedStrings.BackupRecovery_ExitDescription)
        {
            X = 0,
            Y = 9,
        };

        retryChoice.Button.Accepted += (_, _) => retry();
        exitChoice.Button.Accepted += (_, _) => exit();
        Initialized += (_, _) => retryChoice.Button.SetFocus();

        Add(heading, message, issue, retryChoice, exitChoice);
    }
}
