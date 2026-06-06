using Spectre.Console;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalPageLayout
{
    internal const string ShortcutHint = "Shortcuts: ↑/↓ to choose | Esc to cancel | Ctrl + C to exit";
    private const string ReturnToMainMenuPrompt = "Press any key to return to the main menu.";

    private readonly IAnsiConsole _console;

    public TerminalPageLayout(IAnsiConsole console)
    {
        _console = console;
    }

    public void ShowPage(string title, string? description = null, string shortcutHint = ShortcutHint,
        bool clear = true)
    {
        TerminalOutputFormatter.WritePageHeader(_console, title, description, shortcutHint, clear);
    }

    public void ShowReturnHint()
    {
        TerminalOutputFormatter.WriteBottomFooterHint(_console, ReturnToMainMenuPrompt);
    }
}
