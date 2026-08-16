using Terminal.Gui.ViewBase;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class ConfirmationBar : View
{
    public ActionButton ConfirmButton { get; }
    public ActionButton CancelButton { get; }

    private const int GapBetweenButtons = 4;

    public ConfirmationBar(
        string confirmText,
        Action confirmAction,
        string cancelText,
        Action cancelAction,
        ActionKind confirmKind = ActionKind.Default)
    {
        ArgumentNullException.ThrowIfNull(confirmText);
        ArgumentNullException.ThrowIfNull(confirmAction);
        ArgumentNullException.ThrowIfNull(cancelText);
        ArgumentNullException.ThrowIfNull(cancelAction);

        Width = Dim.Fill();
        Height = 1;
        CanFocus = true;

        ConfirmButton = new ActionButton(confirmText, confirmKind)
        {
            X = 0,
            Y = 0,
            Width = Dim.Auto()
        };

        ConfirmButton.Accepted += (_, _) => confirmAction();

        CancelButton = new ActionButton(cancelText, ActionKind.Secondary)
        {
            X = Pos.Right(ConfirmButton) + GapBetweenButtons,
            Y = 0,
            Width = Dim.Auto()
        };
        CancelButton.Accepted += (_, _) => cancelAction();

        Add(ConfirmButton, CancelButton);
    }
}
