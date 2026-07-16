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
        using IApplication application = Terminal.Gui.App.Application.Create().Init();
        var taskRunner = new TerminalTaskRunner(application.Invoke);
        var menuItems = CreateMenuItems(taskRunner);
        using var shell = new TerminalShellView(application, _appInfo, LocalizedStrings.Layout_ShortcutHint);
        using var updateCancellation = new CancellationTokenSource();
        AvailableUpdate? availableUpdate = null;
        MainMenuView? visibleMainMenu = null;

        ShowMainMenu();
        _ = CheckForUpdateAsync();
        application.Run(shell);
        updateCancellation.Cancel();

        return 0;

        void ShowMainMenu()
        {
            var mainMenu = new MainMenuView(menuItems, availableUpdate);
            visibleMainMenu = mainMenu;
            mainMenu.ItemSelected += (_, item) =>
            {
                visibleMainMenu = null;
                shell.ShowContent(item.CreateView(ShowMainMenu));
            };

            shell.ShowContent(mainMenu);
        }

        async Task CheckForUpdateAsync()
        {
            UpdateCheckResult result = await _updateChecker.CheckForUpdateAsync(updateCancellation.Token)
                .ConfigureAwait(false);

            if (result is not UpdateAvailable update)
            {
                return;
            }

            try
            {
                application.Invoke(() =>
                {
                    availableUpdate = update.Update;
                    visibleMainMenu?.ShowAvailableUpdate(update.Update);
                    application.LayoutAndDraw();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }

    private TerminalMenuItem[] CreateMenuItems(TerminalTaskRunner taskRunner)
    {
        return
        [
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_InstallMod_Title,
                LocalizedStrings.MainMenu_InstallMod_Description,
                returnToMainMenu => new InstallModView(
                    _workflowService, _settings, taskRunner, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_UninstallMod_Title,
                LocalizedStrings.MainMenu_UninstallMod_Description,
                returnToMainMenu => new UninstallModView(_workflowService, taskRunner, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_InspectAssets_Title,
                LocalizedStrings.MainMenu_InspectAssets_Description,
                returnToMainMenu => new InspectAssetsView(_workflowService, taskRunner, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_Settings_Title,
                LocalizedStrings.MainMenu_Settings_Description,
                returnToMainMenu => new SettingsView(_settings, returnToMainMenu)),
        ];
    }
}
