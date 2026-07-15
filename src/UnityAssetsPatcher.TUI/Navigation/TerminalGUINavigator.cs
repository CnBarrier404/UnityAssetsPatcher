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
            ITerminalPage? legacyPage = null;
            using IApplication application = Terminal.Gui.App.Application.Create().Init();
            using var shell = new TerminalShellView(application, _appInfo, LocalizedStrings.Layout_ShortcutHint);

            void ShowMainMenu()
            {
                var mainMenu = new MainMenuView(_pages, availableUpdate);
                mainMenu.PageSelected += (_, page) =>
                {
                    if (page is ITerminalGUIPage terminalGUIPage)
                    {
                        shell.ShowContent(terminalGUIPage.CreateView(ShowMainMenu));
                        return;
                    }

                    legacyPage = page;
                    application.RequestStop();
                };
                shell.ShowContent(mainMenu);
            }

            ShowMainMenu();
            application.Run(shell);

            if (legacyPage is null)
            {
                return 0;
            }

            TerminalPageResult result = legacyPage.Run();
            if (result.WaitForKey)
            {
                Console.ReadKey(intercept: true);
            }
        }
    }
}
