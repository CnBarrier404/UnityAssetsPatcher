using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Navigation;

public sealed class TerminalNavigator
{
    private readonly TerminalShellView _shell;
    private readonly LocalizedStrings _strings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;
    private readonly Func<string?> _pickModFile;

    public TerminalNavigator(
        TerminalShellView shell,
        CultureInfo culture,
        IServiceScopeFactory scopeFactory,
        AppRuntimeConfig runtimeConfig,
        ILoggingLevelSwitch? loggingLevelSwitch,
        Func<string?> pickModFile)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(runtimeConfig);
        ArgumentNullException.ThrowIfNull(pickModFile);

        _shell = shell;
        _strings = new LocalizedStrings(culture);
        _scopeFactory = scopeFactory;
        _runtimeConfig = runtimeConfig;
        _loggingLevelSwitch = loggingLevelSwitch;
        _pickModFile = pickModFile;
    }

    public void ShowMainMenu()
    {
        var items = CreateMenuItems();
        var menu = new MainMenuView(_strings, items, _scopeFactory);

        menu.ItemSelected += (_, item) =>
        {
            View content = item.CreateView(ShowMainMenu);

            _shell.ShowContent(content);
        };

        _shell.ShowContent(menu);
    }

    private TerminalMenuItem[] CreateMenuItems()
    {
        return
        [
            new TerminalMenuItem(
                _strings.MainMenu_InstallMod_Title,
                _strings.MainMenu_InstallMod_Description,
                returnToMainMenu => new InstallModView(
                    _strings,
                    _scopeFactory,
                    _runtimeConfig,
                    _pickModFile,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_UninstallMod_Title,
                _strings.MainMenu_UninstallMod_Description,
                returnToMainMenu => new UninstallModView(
                    _strings,
                    _scopeFactory,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_InspectAssets_Title,
                _strings.MainMenu_InspectAssets_Description,
                returnToMainMenu => new InspectAssetsView(
                    _strings,
                    _scopeFactory,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_Settings_Title,
                _strings.MainMenu_Settings_Description,
                returnToMainMenu => new SettingsView(
                    _strings,
                    _runtimeConfig,
                    returnToMainMenu,
                    _loggingLevelSwitch))
        ];
    }
}
