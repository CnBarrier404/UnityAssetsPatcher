using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Pages;

namespace UnityAssetsPatcher.TUI;

internal sealed class TerminalNavigator
{
    private readonly TerminalUI _ui;
    private readonly TerminalPageChrome _chrome;
    private readonly TerminalPrompts _prompts;
    private readonly MainMenuTerminalInput _input;
    private readonly MainMenuTerminalView _view;
    private readonly ITerminalPage[] _pages;
    private int _selectedIndex;

    public TerminalNavigator(
        TerminalUI ui,
        TerminalPageChrome chrome,
        TerminalPrompts prompts,
        MainMenuTerminalInput input,
        MainMenuTerminalView view,
        IEnumerable<ITerminalPage> pages)
    {
        _ui = ui;
        _chrome = chrome;
        _prompts = prompts;
        _input = input;
        _view = view;
        _pages = pages.ToArray();
    }

    public int Run()
    {
        while (true)
        {
            int? selectedIndex = _input.ReadSelection(
                _pages.Length,
                _selectedIndex,
                (index, clear) => _view.WriteMainMenu(_pages, index, clear));

            if (selectedIndex is null)
            {
                return 0;
            }

            _selectedIndex = selectedIndex.Value;
            TerminalPageResult result = RunMenuAction(_pages[_selectedIndex].Run);

            if (!result.WaitForKey)
            {
                continue;
            }

            _chrome.ShowReturnHint();
            _prompts.WaitForKey();
        }
    }

    private TerminalPageResult RunMenuAction(Func<TerminalPageResult> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception)
        {
            _ui.Text.WriteError(exception.Message);
            _prompts.WaitForKey();

            return TerminalPageResult.ReturnToMenu(false);
        }
    }
}
