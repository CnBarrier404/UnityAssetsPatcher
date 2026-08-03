using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalFooterView : View
{
    public TerminalFooterView(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        X = 0;
        Y = Pos.AnchorEnd(1);
        Width = Dim.Fill();
        Height = 1;

        var label = new StyledLabel(text, TextRole.Muted)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
        };

        Add(label);
    }
}
