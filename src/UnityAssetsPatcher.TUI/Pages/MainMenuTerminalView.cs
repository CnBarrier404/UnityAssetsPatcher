using UnityAssetsPatcher.Application.Contracts;
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

    public void WriteMainMenu(
        IReadOnlyList<ITerminalPage> pages,
        int selectedIndex,
        bool clear,
        AvailableUpdate? availableUpdate)
    {
        _ui.Layout.ShowPage(LocalizedStrings.MainMenu_Title, clear: clear);

        if (availableUpdate is not null)
        {
            _ui.Text.WriteMarkupLine(
                $"[{TerminalTheme.StatusPreview}]{TerminalText.Escape(string.Format(LocalizedStrings.Update_AvailableFormat, availableUpdate.Version))}[/]");
            _ui.Text.WriteInfo(string.Format(LocalizedStrings.Update_DownloadFormat, availableUpdate.ReleaseUrl));
            _ui.Text.WriteBlankLine();
        }

        _ui.List.WriteDescribedList(
            pages.Select(page => new TerminalChoiceDisplay(page.Title, page.Description)).ToArray(),
            selectedIndex);
    }
}
