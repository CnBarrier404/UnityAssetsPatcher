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
        : this(
            confirmText,
            ToAsync(confirmAction),
            cancelText,
            ToAsync(cancelAction),
            confirmKind) { }

    public ConfirmationBar(
        string confirmText,
        Func<Task> confirmAction,
        string cancelText,
        Action cancelAction,
        ActionKind confirmKind = ActionKind.Default)
        : this(
            confirmText,
            confirmAction,
            cancelText,
            ToAsync(cancelAction),
            confirmKind) { }

    public ConfirmationBar(
        string confirmText,
        Func<Task> confirmAction,
        string cancelText,
        Func<Task> cancelAction,
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

        ConfirmButton.Accepted += async (_, _) => await confirmAction();

        CancelButton = new ActionButton(cancelText, ActionKind.Secondary)
        {
            X = Pos.Right(ConfirmButton) + GapBetweenButtons,
            Y = 0,
            Width = Dim.Auto()
        };
        CancelButton.Accepted += async (_, _) => await cancelAction();

        Add(ConfirmButton, CancelButton);
    }

    private static Func<Task> ToAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return () =>
        {
            action();
            return Task.CompletedTask;
        };
    }
}
