using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI;

internal sealed class TerminalPageChrome
{
    private readonly TerminalUI _ui;

    public TerminalPageChrome(TerminalUI ui)
    {
        _ui = ui;
    }

    public void ShowPage(string title, string? description = null, string? shortcutHint = null, bool clear = true)
    {
        _ui.Layout.ShowPage(
            title,
            description,
            shortcutHint: shortcutHint ?? TerminalLayout.ShortcutHint,
            clear);
    }

    public void PrepareOutputArea()
    {
        _ui.Layout.PrepareOutputArea();
    }

    public void ShowShortcutHint()
    {
        _ui.Layout.ShowShortcutHint();
    }

    public void ShowReturnHint()
    {
        _ui.Layout.ShowReturnHint();
    }
}
