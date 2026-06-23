using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class MainMenuTerminalView
{
    private readonly TerminalUI _ui;

    public MainMenuTerminalView(TerminalUI ui)
    {
        _ui = ui;
    }

    public void WriteMainMenu(IReadOnlyList<ITerminalPage> pages, int selectedIndex, bool clear)
    {
        _ui.Layout.ShowPage(LocalizedStrings.MainMenu_Title, clear: clear);
        _ui.List.WriteDescribedList(
            pages.Select(page => new TerminalChoiceDisplay(page.Title, page.Description)).ToArray(),
            selectedIndex);
    }
}
