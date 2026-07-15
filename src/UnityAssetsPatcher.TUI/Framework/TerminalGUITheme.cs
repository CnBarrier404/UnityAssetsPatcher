using Terminal.Gui.Drawing;
using TerminalAttribute = Terminal.Gui.Drawing.Attribute;

namespace UnityAssetsPatcher.TUI.Framework;

public static class TerminalGUITheme
{
    public static Scheme Base { get; } = Create(Color.None);

    public static Scheme Muted { get; } = Create(new Color("#5c6370"));

    public static Scheme Selected { get; } = Create(new Color("#c678dd"));

    public static Scheme Title { get; } = Create(new Color("#61afef"), TextStyle.Bold);

    public static Scheme Preview { get; } = Create(new Color("#e5c07b"), TextStyle.Bold);

    private static Scheme Create(Color foreground, TextStyle style = TextStyle.None)
    {
        var attribute = new TerminalAttribute(foreground, Color.None, style);

        return new Scheme
        {
            Normal = attribute,
            Focus = attribute,
            HotNormal = attribute,
            HotFocus = attribute,
            Active = attribute,
        };
    }
}
