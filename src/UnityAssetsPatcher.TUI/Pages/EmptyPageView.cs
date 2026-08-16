using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class EmptyPageView : View
{
    public ActionButton BackButton { get; }

    public EmptyPageView(string title, string backText, Action returnToMainMenu)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(backText);
        ArgumentNullException.ThrowIfNull(returnToMainMenu);

        KeyDown += (_, key) =>
        {
            if (key != Key.Esc)
            {
                return;
            }

            key.Handled = true;

            returnToMainMenu();
        };

        var heading = new StyledLabel(title, TextRole.Title)
        {
            X = 0,
            Y = 0
        };

        BackButton = new ActionButton(backText, ActionKind.Secondary)
        {
            X = 0,
            Y = 2
        };
        BackButton.Accepted += (_, _) => returnToMainMenu();

        Add(heading, BackButton);

        Initialized += (_, _) => BackButton.SetFocus();
    }
}
