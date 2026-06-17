using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class MainMenuTerminalPage
{
    private readonly TerminalAppContext _context;
    private readonly IReadOnlyList<ITerminalPage> _pages;
    private int _selectedIndex;

    public MainMenuTerminalPage(
        TerminalAppContext context,
        IReadOnlyList<ITerminalPage> pages)
    {
        _context = context;
        _pages = pages;
    }

    public ITerminalPage? ReadSelection()
    {
        int? selectedIndex = _context.Prompts.ReadChoiceIndex(_pages.Count, _selectedIndex, WriteMainMenu);

        if (selectedIndex is null)
        {
            return null;
        }

        _selectedIndex = selectedIndex.Value;

        return _pages[_selectedIndex];
    }

    private void WriteMainMenu(int selectedIndex, bool clear)
    {
        _context.Ui.Layout.ShowPage("Main menu", clear: clear);
        _context.Ui.Lists.WriteDescribedChoiceList(
            _pages.Select(page => new TerminalChoiceDisplay(page.Title, page.Description)).ToArray(),
            selectedIndex);
    }
}
