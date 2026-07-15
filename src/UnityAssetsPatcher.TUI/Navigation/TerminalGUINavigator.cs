using Terminal.Gui.App;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Navigation;

public sealed class TerminalGUINavigator
{
    private readonly AppInfo _appInfo;
    private readonly IUpdateChecker _updateChecker;
    private readonly ITerminalPage[] _pages;

    public TerminalGUINavigator(
        AppInfo appInfo,
        IUpdateChecker updateChecker,
        IEnumerable<ITerminalPage> pages)
    {
        _appInfo = appInfo;
        _updateChecker = updateChecker;
        _pages = pages.ToArray();
    }

    public int Run()
    {
        AvailableUpdate? availableUpdate = _updateChecker.CheckForUpdate();

        while (true)
        {
            ITerminalPage? selectedPage = RunMainMenu(availableUpdate);

            if (selectedPage is null)
            {
                return 0;
            }

            TerminalPageResult result = selectedPage.Run();

            if (result.WaitForKey)
            {
                Console.ReadKey(intercept: true);
            }
        }
    }

    private ITerminalPage? RunMainMenu(AvailableUpdate? availableUpdate)
    {
        ITerminalPage? selectedPage = null;

        using IApplication application = Terminal.Gui.App.Application.Create().Init();
        using var shell = new TerminalShellView(_appInfo, LocalizedStrings.Layout_ShortcutHint);
        var mainMenu = new MainMenuView(_pages, availableUpdate);
        mainMenu.PageSelected += (_, page) =>
        {
            selectedPage = page;
            application.RequestStop();
        };
        shell.ShowContent(mainMenu);
        application.Run(shell);

        return selectedPage;
    }
}
