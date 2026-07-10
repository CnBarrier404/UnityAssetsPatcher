using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Pages;

namespace UnityAssetsPatcher.TUI;

public static class TerminalServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherTUI(
        this IServiceCollection services,
        AppInfo appInfo,
        IAnsiConsole console)
    {
        services.AddSingleton(console);
        services.AddSingleton(_ => new TerminalUI(console, appInfo));
        services.AddSingleton(provider => new TerminalPrompts(
            console,
            provider.GetRequiredService<TerminalUI>().Text));
        services.AddSingleton<TerminalPageChrome>();
        services.AddSingleton<TerminalSettings>();

        services.AddSingleton<ITerminalPage, InstallTerminalPage>();
        services.AddSingleton<ITerminalPage, UninstallTerminalPage>();
        services.AddSingleton<ITerminalPage, SettingsTerminalPage>();
        services.AddSingleton<InstallTerminalInput>();
        services.AddSingleton<InstallTerminalView>();
        services.AddSingleton<UninstallTerminalInput>();
        services.AddSingleton<UninstallTerminalView>();
        services.AddSingleton<SettingsTerminalInput>();
        services.AddSingleton<SettingsTerminalView>();
        services.AddSingleton<MainMenuTerminalInput>();
        services.AddSingleton<MainMenuTerminalView>();
        services.AddSingleton<TerminalNavigator>();
        services.AddSingleton(provider => new TerminalApp(
            provider.GetRequiredService<IAnsiConsole>(),
            provider.GetRequiredService<TerminalUI>(),
            provider.GetRequiredService<TerminalNavigator>()));

        return services;
    }
}
