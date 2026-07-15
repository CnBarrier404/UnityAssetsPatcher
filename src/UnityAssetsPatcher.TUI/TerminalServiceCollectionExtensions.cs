using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Pages;

namespace UnityAssetsPatcher.TUI;

public static class TerminalServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherTUI(
        this IServiceCollection services,
        AppInfo appInfo,
        IAnsiConsole console)
    {
        services.AddSingleton(appInfo);
        services.AddSingleton(console);
        services.AddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3),
        });
        services.AddSingleton<IUpdateChecker>(provider => new GitHubUpdateChecker(
            provider.GetRequiredService<HttpClient>(),
            provider.GetRequiredService<AppInfo>()));
        services.AddSingleton(_ => new TerminalUI(console, appInfo));
        services.AddSingleton(provider => new TerminalPrompts(
            console,
            provider.GetRequiredService<TerminalUI>().Text));
        services.AddSingleton<TerminalPageChrome>();
        services.AddSingleton<TerminalSettings>();

        services.AddSingleton<ITerminalPage, InstallTerminalPage>();
        services.AddSingleton<ITerminalPage, UninstallTerminalPage>();
        services.AddSingleton<ITerminalPage, InspectTerminalPage>();
        services.AddSingleton<ITerminalPage, SettingsTerminalPage>();
        services.AddSingleton<UninstallTerminalInput>();
        services.AddSingleton<UninstallTerminalView>();
        services.AddSingleton<InspectTerminalInput>();
        services.AddSingleton<InspectTerminalView>();
        services.AddSingleton<TerminalGUINavigator>();
        services.AddSingleton(provider => new TerminalApp(
            provider.GetRequiredService<IAnsiConsole>(),
            provider.GetRequiredService<TerminalUI>(),
            provider.GetRequiredService<TerminalGUINavigator>()));

        return services;
    }
}
