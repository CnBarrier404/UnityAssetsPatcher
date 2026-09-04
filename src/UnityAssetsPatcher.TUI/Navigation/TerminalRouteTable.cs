using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Pages.InstallMod;
using UnityAssetsPatcher.TUI.Pages.MainMenu;
using UnityAssetsPatcher.TUI.Pages.Settings;

namespace UnityAssetsPatcher.TUI.Navigation;

public sealed class TerminalRouteTable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly MainMenuLogic _mainMenuLogic;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;

    public TerminalRouteTable(
        IServiceScopeFactory scopeFactory,
        AppRuntimeConfig runtimeConfig,
        MainMenuLogic mainMenuLogic,
        ILoggingLevelSwitch? loggingLevelSwitch = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(runtimeConfig);
        ArgumentNullException.ThrowIfNull(mainMenuLogic);

        _scopeFactory = scopeFactory;
        _runtimeConfig = runtimeConfig;
        _mainMenuLogic = mainMenuLogic;
        _loggingLevelSwitch = loggingLevelSwitch;
    }

    public IReadOnlyDictionary<TerminalRoute, Func<TerminalPageView>> Create(
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var strings = new LocalizedStrings(culture);
        MainMenuItem[] menuItems =
        [
            new(
                strings.MainMenu_InstallMod_Title,
                strings.MainMenu_InstallMod_Description,
                TerminalRoute.InstallMod),
            new(
                strings.MainMenu_UninstallMod_Title,
                strings.MainMenu_UninstallMod_Description,
                TerminalRoute.UninstallMod),
            new(
                strings.MainMenu_InspectAssets_Title,
                strings.MainMenu_InspectAssets_Description,
                TerminalRoute.InspectAssets),
            new(
                strings.MainMenu_Settings_Title,
                strings.MainMenu_Settings_Description,
                TerminalRoute.Settings)
        ];

        return new Dictionary<TerminalRoute, Func<TerminalPageView>>
        {
            [TerminalRoute.MainMenu] = () =>
                new MainMenuView(strings, menuItems, _mainMenuLogic),
            [TerminalRoute.InstallMod] = () =>
                new InstallModView(
                    strings,
                    new InstallModLogic(_scopeFactory, _runtimeConfig)),
            [TerminalRoute.UninstallMod] = () =>
                new UninstallModView(strings, _scopeFactory),
            [TerminalRoute.InspectAssets] = () =>
                new InspectAssetsView(strings, _scopeFactory),
            [TerminalRoute.Settings] = () =>
                new SettingsView(strings, new SettingsLogic(_runtimeConfig, _loggingLevelSwitch))
        };
    }
}
