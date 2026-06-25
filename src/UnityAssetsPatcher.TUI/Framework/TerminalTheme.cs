namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalTheme
{
    public static string Title => "bold #61afef";

    public static string Muted => "#5c6370";

    public static string Label => "#61afef";

    public static string SectionHeader => "bold #56b6c2";

    public static string Selection => "#c678dd";

    public static string Warning => "#e5c07b";

    public static string Error => "#e06c75";

    public static string StatusPreview => "bold #e5c07b";

    public static string StatusSuccess => "bold #98c379";

    public static TerminalTheme Default { get; } = new();
}
