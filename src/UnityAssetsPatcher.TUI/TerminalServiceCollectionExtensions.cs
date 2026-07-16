using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Navigation;

namespace UnityAssetsPatcher.TUI;

public static class TerminalServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherTUI(
        this IServiceCollection services,
        AppInfo appInfo)
    {
        services.AddSingleton(appInfo);
        services.AddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        });
        services.AddSingleton<IUpdateChecker>(provider => new GitHubUpdateChecker(
            provider.GetRequiredService<HttpClient>(),
            provider.GetRequiredService<AppInfo>()));
        services.AddSingleton<TerminalSettings>();

        services.AddSingleton<TerminalGUINavigator>();
        services.AddSingleton<TerminalApp>();

        return services;
    }
}
