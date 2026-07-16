using Terminal.Gui.ViewBase;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class ChoiceItem : View
{
    public ActionButton Button { get; }
    public StyledLabel Description { get; }

    public ChoiceItem(string text, string description)
    {
        Width = Dim.Fill();
        Height = 1;
        CanFocus = true;
        Button = new ActionButton(text) { X = 0, Y = 0, Width = 30 };

        Description = new StyledLabel(description, TextRole.Muted)
        {
            X = 36,
            Y = 0,
            Width = Dim.Fill(),
        };

        Button.HasFocusChanged += (_, _) =>
        {
            Description.SetRole(Button.HasFocus ? TextRole.Selected : TextRole.Muted);
        };

        Add(Button, Description);
    }
}
