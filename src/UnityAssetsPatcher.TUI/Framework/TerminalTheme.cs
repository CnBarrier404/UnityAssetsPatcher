namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalTheme
{
    public static string Title => "bold blue";

    public static string Muted => "grey";

    public static string Label => "blue";

    public static string SectionHeader => "bold blue";

    public static string Selection => "cyan";

    public static string Warning => "yellow";

    public static string Error => "red";

    public static string StatusPreview => "bold yellow";

    public static string StatusSuccess => "bold green";

    public static TerminalTheme Default { get; } = new();
}
