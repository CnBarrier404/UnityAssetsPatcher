using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Pages.Settings;

namespace UnityAssetsPatcher.TUI.Navigation;

public static class TerminalRouteTable
{
    public static IReadOnlyDictionary<TerminalRoute, Func<TerminalPageView>> Create(
        CultureInfo culture,
        IServiceScopeFactory scopeFactory,
        AppRuntimeConfig runtimeConfig,
        ILoggingLevelSwitch? loggingLevelSwitch)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(runtimeConfig);

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
                new MainMenuView(strings, menuItems, scopeFactory),
            [TerminalRoute.InstallMod] = () =>
                new InstallModView(strings, scopeFactory, runtimeConfig),
            [TerminalRoute.UninstallMod] = () =>
                new UninstallModView(strings, scopeFactory),
            [TerminalRoute.InspectAssets] = () =>
                new InspectAssetsView(strings, scopeFactory),
            [TerminalRoute.Settings] = () =>
                new SettingsView(strings, new SettingsLogic(runtimeConfig, loggingLevelSwitch))
        };
    }
}
