namespace UnityAssetsPatcher.Tui.Pages;

internal sealed class MainMenuTerminalPage
{
    private readonly TerminalAppContext _context;
    private readonly TerminalSelectionPrompt _selectionPrompt;
    private readonly IReadOnlyList<ITerminalPage> _pages;
    private int _selectedIndex;

    public MainMenuTerminalPage(
        TerminalAppContext context,
        TerminalSelectionPrompt selectionPrompt,
        IReadOnlyList<ITerminalPage> pages)
    {
        _context = context;
        _selectionPrompt = selectionPrompt;
        _pages = pages;
    }

    public ITerminalPage? ReadSelection()
    {
        int? selectedIndex = _selectionPrompt.ReadSelection(_pages.Count, _selectedIndex, WriteMainMenu);

        if (selectedIndex is null)
        {
            return null;
        }

        _selectedIndex = selectedIndex.Value;

        return _pages[_selectedIndex];
    }

    private void WriteMainMenu(int selectedIndex, bool clear)
    {
        _context.Layout.ShowPage("Main menu", clear: clear);
        TerminalOutputFormatter.WriteMainMenu(_context.Console, _pages, selectedIndex);
    }
}
