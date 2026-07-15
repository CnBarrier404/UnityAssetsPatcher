using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalFooterView : View
{
    private readonly Label _label;

    public TerminalFooterView(string text)
    {
        X = 0;
        Y = Pos.AnchorEnd(1);
        Width = Dim.Fill();
        Height = 1;

        _label = new Label { Text = text, X = 0, Y = 0, Width = Dim.Fill() };
        _label.SetScheme(TerminalGUITheme.Muted);
        Add(_label);
    }

    public void SetText(string text)
    {
        _label.Text = text;
    }
}
