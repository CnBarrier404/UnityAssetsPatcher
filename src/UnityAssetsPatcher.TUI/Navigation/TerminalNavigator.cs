using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Navigation;

public sealed class TerminalNavigator : ITerminalContentHost
{
    private readonly TerminalShellView _shell;
    private readonly LocalizedStrings _strings;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly TerminalSettings? _settings;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;
    private readonly TerminalTaskRunner? _taskRunner;
    private readonly Func<string?> _pickModFile;
    private UpdateInfo? _availableUpdate;
    private MainMenuView? _visibleMainMenu;

    public TerminalNavigator(TerminalShellView shell, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(culture);

        _shell = shell;
        _strings = new LocalizedStrings(culture);
        _pickModFile = static () => null;
    }

    public TerminalNavigator(
        TerminalShellView shell,
        CultureInfo culture,
        IServiceScopeFactory scopeFactory,
        TerminalSettings settings,
        ILoggingLevelSwitch? loggingLevelSwitch,
        TerminalTaskRunner taskRunner,
        Action requestStop,
        Func<string?> pickModFile)
        : this(shell, culture)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(taskRunner);
        ArgumentNullException.ThrowIfNull(requestStop);
        ArgumentNullException.ThrowIfNull(pickModFile);

        _scopeFactory = scopeFactory;
        _settings = settings;
        _loggingLevelSwitch = loggingLevelSwitch;
        _taskRunner = taskRunner;
        _pickModFile = pickModFile;
    }

    public void ShowContent(View content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _shell.ShowContent(content);
    }

    public void ShowMainMenu()
    {
        var items = CreateMenuItems();
        TerminalUpdateNotice? updateNotice = _availableUpdate is null
            ? null
            : CreateUpdateNotice(_availableUpdate);
        var menu = new MainMenuView(_strings.MainMenu_Title, items, updateNotice);

        _visibleMainMenu = menu;

        menu.ItemSelected += (_, item) =>
        {
            _visibleMainMenu = null;

            View content = item.CreateView(ShowMainMenu);

            _shell.ShowContent(content);
        };

        ShowContent(menu);
    }

    public void ShowAvailableUpdate(UpdateInfo update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _availableUpdate = update;

        _visibleMainMenu?.ShowAvailableUpdate(CreateUpdateNotice(update));
    }

    private TerminalMenuItem[] CreateMenuItems()
    {
        if (_scopeFactory is null || _settings is null || _taskRunner is null)
        {
            return
            [
                CreateEmptyPageMenuItem(
                    _strings.MainMenu_InstallMod_Title,
                    _strings.MainMenu_InstallMod_Description),
                CreateEmptyPageMenuItem(
                    _strings.MainMenu_UninstallMod_Title,
                    _strings.MainMenu_UninstallMod_Description),
                CreateEmptyPageMenuItem(
                    _strings.MainMenu_InspectAssets_Title,
                    _strings.MainMenu_InspectAssets_Description),
                CreateEmptyPageMenuItem(
                    _strings.MainMenu_Settings_Title,
                    _strings.MainMenu_Settings_Description)
            ];
        }

        return
        [
            new TerminalMenuItem(
                _strings.MainMenu_InstallMod_Title,
                _strings.MainMenu_InstallMod_Description,
                returnToMainMenu => new InstallModView(
                    _strings,
                    _scopeFactory,
                    _settings,
                    _taskRunner,
                    _pickModFile,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_UninstallMod_Title,
                _strings.MainMenu_UninstallMod_Description,
                returnToMainMenu => new UninstallModView(
                    _strings,
                    _scopeFactory,
                    _taskRunner,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_InspectAssets_Title,
                _strings.MainMenu_InspectAssets_Description,
                returnToMainMenu => new InspectAssetsView(
                    _strings,
                    _scopeFactory,
                    _taskRunner,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_Settings_Title,
                _strings.MainMenu_Settings_Description,
                returnToMainMenu => new SettingsView(
                    _strings,
                    _settings,
                    returnToMainMenu,
                    _loggingLevelSwitch))
        ];
    }

    private TerminalMenuItem CreateEmptyPageMenuItem(string title, string description)
    {
        return new TerminalMenuItem(
            title,
            description,
            returnToMainMenu => new EmptyPageView(title, _strings.EmptyPage_BackAction, returnToMainMenu));
    }

    private TerminalUpdateNotice CreateUpdateNotice(UpdateInfo update)
    {
        return new TerminalUpdateNotice(
            _strings.Update_AvailableFormat(update.Version),
            _strings.Update_DownloadFormat(update.ReleaseUrl));
    }
}
