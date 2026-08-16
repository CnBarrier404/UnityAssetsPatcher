using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class ChoiceItemList : View
{
    public ActionButton Button { get; }
    public StyledLabel Description { get; }

    private const int DescriptionGap = 4;

    public ChoiceItemList(string text, string description)
    {
        Width = Dim.Fill();
        Height = 1;
        CanFocus = true;

        Button = new ActionButton(text)
        {
            X = 0,
            Y = 0,
            Width = Dim.Auto()
        };

        Description = new StyledLabel(description, TextRole.Muted)
        {
            X = Pos.Right(Button) + DescriptionGap,
            Y = 0,
            Width = Dim.Fill()
        };

        Button.HasFocusChanged += (_, _) =>
        {
            Description.SetRole(Button.HasFocus ? TextRole.Selected : TextRole.Muted);
        };

        Add(Button, Description);
    }

    public static void AlignDescriptions(IReadOnlyList<ChoiceItemList> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return;
        }

        int width = items.Max(item => item.Button.Text.GetColumns());

        foreach (ChoiceItemList item in items)
        {
            item.Button.Width = width;
        }
    }
}
