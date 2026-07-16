using Terminal.Gui.Drawing;
using TerminalAttribute = Terminal.Gui.Drawing.Attribute;

namespace UnityAssetsPatcher.TUI.Framework;

public static class TerminalTheme
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
    public static Scheme Interactive { get; } = CreateInteractive(Base.Normal);
    public static Scheme PrimaryAction { get; } = CreateInteractive(Label.Normal);
    public static Scheme SecondaryAction { get; } = CreateInteractive(Muted.Normal);
    public static Scheme DangerousAction { get; } = CreateInteractive(Error.Normal);

    public static Scheme GetTextScheme(TextRole role) => role switch
    {
        TextRole.Base => Base,
        TextRole.Muted => Muted,
        TextRole.Selected => Selected,
        TextRole.Title => Title,
        TextRole.Label => Label,
        TextRole.SectionHeader => SectionHeader,
        TextRole.Preview => Preview,
        TextRole.Error => Error,
        TextRole.Success => Success,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static Scheme CreateInteractive(TerminalAttribute normal)
    {
        TerminalAttribute selected = Selected.Normal;

        return new Scheme
        {
            Normal = normal,
            Focus = selected,
            HotNormal = normal,
            HotFocus = selected,
            Active = selected,
            Editable = normal,
            ReadOnly = normal,
            Disabled = normal,
        };
    }

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
