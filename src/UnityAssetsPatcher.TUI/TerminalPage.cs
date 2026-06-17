using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI;

internal abstract class TerminalPage : ITerminalPage
{
    protected TerminalAppContext Context { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }

    protected TerminalPage(TerminalAppContext context)
    {
        Context = context;
    }

    public abstract TerminalPageResult Run();

    protected void NewPage(string? title = null, string? description = null, string? shortcutHint = null,
        bool clear = true)
    {
        Context.Ui.Layout.ShowPage(
            title ?? Title,
            description ?? Description,
            shortcutHint: shortcutHint ?? TerminalLayout.ShortcutHint,
            clear);
    }
}
