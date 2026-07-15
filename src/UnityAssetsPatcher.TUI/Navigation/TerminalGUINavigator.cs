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
    private readonly IWorkflowService _workflowService;
    private readonly TerminalSettings _settings;

    public TerminalGUINavigator(
        AppInfo appInfo,
        IUpdateChecker updateChecker,
        IWorkflowService workflowService,
        TerminalSettings settings)
    {
        _appInfo = appInfo;
        _updateChecker = updateChecker;
        _workflowService = workflowService;
        _settings = settings;
    }

    public int Run()
    {
        AvailableUpdate? availableUpdate = _updateChecker.CheckForUpdate();
        TerminalMenuItem[] menuItems = CreateMenuItems();
        using IApplication application = Terminal.Gui.App.Application.Create().Init();
        using var shell = new TerminalShellView(application, _appInfo, LocalizedStrings.Layout_ShortcutHint);

        void ShowMainMenu()
        {
            var mainMenu = new MainMenuView(menuItems, availableUpdate);
            mainMenu.ItemSelected += (_, item) => { shell.ShowContent(item.CreateView(ShowMainMenu)); };
            shell.ShowContent(mainMenu);
        }

        ShowMainMenu();
        application.Run(shell);
        return 0;
    }

    private TerminalMenuItem[] CreateMenuItems()
    {
        return
        [
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_InstallMod_Title,
                LocalizedStrings.MainMenu_InstallMod_Description,
                returnToMainMenu => new InstallModView(_workflowService, _settings, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_UninstallMod_Title,
                LocalizedStrings.MainMenu_UninstallMod_Description,
                returnToMainMenu => new UninstallModView(_workflowService, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_InspectAssets_Title,
                LocalizedStrings.MainMenu_InspectAssets_Description,
                returnToMainMenu => new InspectAssetsView(_workflowService, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_Settings_Title,
                LocalizedStrings.MainMenu_Settings_Description,
                returnToMainMenu => new SettingsView(_settings, returnToMainMenu)),
        ];
    }
}
