using Terminal.Gui.Drawing;
using TerminalAttribute = Terminal.Gui.Drawing.Attribute;

namespace UnityAssetsPatcher.TUI.Framework;

public static class TerminalGUITheme
{
    public static Scheme Base { get; } = Create(Color.None);

    public static Scheme Muted { get; } = Create(new Color("#5c6370"));

    public static Scheme Selected { get; } = Create(new Color("#c678dd"));

    public static Scheme Title { get; } = Create(new Color("#61afef"), TextStyle.Bold);

    public static Scheme Label { get; } = Create(new Color("#61afef"));

    public static Scheme SectionHeader { get; } = Create(new Color("#56b6c2"), TextStyle.Bold);

    public static Scheme Preview { get; } = Create(new Color("#e5c07b"), TextStyle.Bold);

    public static Scheme Error { get; } = Create(new Color("#e06c75"));

    public static Scheme Success { get; } = Create(new Color("#98c379"), TextStyle.Bold);

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
            Editable = attribute,
            ReadOnly = attribute,
            Disabled = attribute,
        };
    }
}
