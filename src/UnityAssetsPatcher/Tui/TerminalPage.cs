namespace UnityAssetsPatcher.Tui;

internal abstract class TerminalPage
{
    protected TerminalAppContext Context { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }

    protected TerminalPage(TerminalAppContext context)
    {
        Context = context;
    }

    public abstract bool Run();

    protected void NewPage(string? title = null, string? description = null, string? shortcutHint = null,
        bool clear = true)
    {
        Context.Layout.ShowPage(
            title ?? Title,
            description ?? Description,
            shortcutHint: shortcutHint ?? TerminalPageLayout.ShortcutHint,
            clear);
    }
}
