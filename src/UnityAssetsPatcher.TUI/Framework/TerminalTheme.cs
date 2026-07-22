using Terminal.Gui.Drawing;
using TerminalAttribute = Terminal.Gui.Drawing.Attribute;

namespace UnityAssetsPatcher.TUI.Framework;

public static class TerminalTheme
{
    public static Scheme Base { get; private set; } = null!;
    public static Scheme Muted { get; private set; } = null!;
    public static Scheme Selected { get; private set; } = null!;
    public static Scheme Title { get; private set; } = null!;
    public static Scheme Label { get; private set; } = null!;
    public static Scheme SectionHeader { get; private set; } = null!;
    public static Scheme Preview { get; private set; } = null!;
    public static Scheme Error { get; private set; } = null!;
    public static Scheme Success { get; private set; } = null!;
    public static Scheme Interactive { get; private set; } = null!;
    public static Scheme PrimaryAction { get; private set; } = null!;
    public static Scheme SecondaryAction { get; private set; } = null!;
    public static Scheme DangerousAction { get; private set; } = null!;

    static TerminalTheme()
    {
        Initialize(false);
    }

    public static void Initialize(bool useLegacyConsoleColors)
    {
        Color background = useLegacyConsoleColors ? new Color("#000000") : Color.None;
        Color baseForeground = useLegacyConsoleColors ? new Color("#abb2bf") : Color.None;

        Base = Create(baseForeground, background);
        Muted = Create(new Color("#5c6370"), background);
        Selected = Create(new Color("#c678dd"), background);
        Title = Create(new Color("#61afef"), background, TextStyle.Bold);
        Label = Create(new Color("#61afef"), background);
        SectionHeader = Create(new Color("#56b6c2"), background, TextStyle.Bold);
        Preview = Create(new Color("#e5c07b"), background, TextStyle.Bold);
        Error = Create(new Color("#e06c75"), background);
        Success = Create(new Color("#98c379"), background, TextStyle.Bold);
        Interactive = CreateInteractive(Base.Normal);
        PrimaryAction = CreateInteractive(Label.Normal);
        SecondaryAction = CreateInteractive(Muted.Normal);
        DangerousAction = CreateInteractive(Error.Normal);
    }

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

    private static Scheme CreateInteractive(TerminalAttribute normal)
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

    private static Scheme Create(Color foreground, Color background, TextStyle style = TextStyle.None)
    {
        var attribute = new TerminalAttribute(foreground, background, style);

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
