using Terminal.Gui.Views;

namespace UnityAssetsPatcher.TUI.Framework;

#pragma warning disable CS0618
// Terminal.Gui has no bundled read-only, scrollable text viewer.
public sealed class TextViewer : TextView
{
    public TextViewer(string text = "", TextRole role = TextRole.Base)
    {
        ReadOnly = true;
        Text = text;
        SetScheme(TerminalTheme.GetTextScheme(role));
    }
}
#pragma warning restore CS0618
