using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.TUI.Hooks;
using UnityAssetsPatcher.TUI.Lifecycle;

namespace UnityAssetsPatcher.TUI;

public static class TerminalServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherTUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TerminalSettings>();
        services.AddSingleton<TerminalApp>();
        services.AddScoped<TerminalLifecycle>();
        services.AddScoped<ITerminalStartupHook, RepositoryInitializationStartupHook>();
        services.AddScoped<ITerminalSessionHook, UpdateCheckSessionHook>();
        services.AddScoped<TerminalSession>();

        return services;
    }
}
