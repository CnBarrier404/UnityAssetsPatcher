using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class UnsupportedRepositoryView : View
{
    internal UnsupportedRepositoryView(
        LocalizedStrings strings,
        string actualVersion,
        string supportedVersion,
        string? failure,
        Action clear,
        Action exit)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(actualVersion);
        ArgumentNullException.ThrowIfNull(supportedVersion);
        ArgumentNullException.ThrowIfNull(clear);
        ArgumentNullException.ThrowIfNull(exit);

        Add(new StyledLabel(strings.RepositoryFormat_UnsupportedTitle, TextRole.Error)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        });
        Add(new StyledLabel(strings.RepositoryFormat_UnsupportedDescription, TextRole.Preview)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill()
        });
        Add(new StyledLabel(
            strings.RepositoryFormat_UnsupportedVersionFormat(actualVersion, supportedVersion),
            TextRole.Error)
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill()
        });
        Add(new StyledLabel(strings.RepositoryFormat_ClearWarning, TextRole.Error)
        {
            X = 0,
            Y = 6,
            Width = Dim.Fill()
        });

        int actionRow = 9;
        if (!string.IsNullOrWhiteSpace(failure))
        {
            Add(new StyledLabel(strings.RepositoryFormat_ClearFailedFormat(failure), TextRole.Error)
            {
                X = 0,
                Y = 8,
                Width = Dim.Fill()
            });
            actionRow = 11;
        }

        var clearChoice = new ChoiceItem(
            strings.RepositoryFormat_ClearAction,
            strings.RepositoryFormat_ClearDescription)
        {
            X = 0,
            Y = actionRow
        };
        clearChoice.Button.Accepted += (_, _) => clear();

        var exitChoice = new ChoiceItem(
            strings.RepositoryFormat_ExitAction,
            strings.RepositoryFormat_ExitDescription)
        {
            X = 0,
            Y = actionRow + 2
        };
        exitChoice.Button.Accepted += (_, _) => exit();

        Add(clearChoice, exitChoice);
        ChoiceItem.AlignDescriptions([clearChoice, exitChoice]);
        Initialized += (_, _) => clearChoice.Button.SetFocus();
    }
}

public sealed class ClearUnsupportedRepositoryConfirmationView : View
{
    internal ClearUnsupportedRepositoryConfirmationView(
        LocalizedStrings strings,
        Func<Task> confirm,
        Action cancel)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(cancel);

        Add(new StyledLabel(strings.RepositoryFormat_ConfirmTitle, TextRole.Error)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        });
        Add(new StyledLabel(strings.RepositoryFormat_ConfirmDescription, TextRole.Error)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill()
        });

        var actions = new ConfirmationBar(
            strings.RepositoryFormat_ConfirmAction,
            confirm,
            strings.RepositoryFormat_CancelAction,
            cancel,
            ActionKind.Dangerous)
        {
            X = 0,
            Y = 5
        };
        Add(actions);
        Initialized += (_, _) => actions.CancelButton.SetFocus();
    }
}
