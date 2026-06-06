namespace UnityAssetsPatcher.Tui.Pages;

internal sealed class MainMenuTerminalPage
{
    private readonly TerminalAppContext _context;
    private readonly InteractivePrompts _prompts;
    private readonly IReadOnlyList<TerminalPage> _pages;

    public MainMenuTerminalPage(
        TerminalAppContext context,
        InteractivePrompts prompts,
        IReadOnlyList<TerminalPage> pages)
    {
        _context = context;
        _prompts = prompts;
        _pages = pages;
    }

    public TerminalPage? ReadSelection()
    {
        int? selectedIndex = _prompts.ReadMainMenuChoice(_pages, WriteMainMenu, WriteMainHeader);

        return selectedIndex is null ? null : _pages[selectedIndex.Value];
    }

    private void WriteMainHeader()
    {
        _context.Layout.ShowPage("Main menu");
    }

    private void WriteMainMenu(int selectedIndex, bool clear)
    {
        _context.Layout.ShowPage("Main menu", clear: clear);
        TerminalOutputFormatter.WriteMainMenu(_context.Console, _pages, selectedIndex);
    }
}
