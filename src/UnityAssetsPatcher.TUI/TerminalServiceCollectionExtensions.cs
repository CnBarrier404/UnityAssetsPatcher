using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.TUI.Lifecycle;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Pages.MainMenu;

namespace UnityAssetsPatcher.TUI;

public static class TerminalServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherTUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TerminalApp>();
        services.AddScoped<TerminalSession>();
        services.AddScoped<TerminalRouteTable>();
        services.AddScoped<MainMenuLogic>();

        return services;
    }
}
