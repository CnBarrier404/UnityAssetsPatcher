using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.TUI.Lifecycle;

namespace UnityAssetsPatcher.TUI;

public static class TerminalServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherTUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TerminalApp>();
        services.AddScoped<TerminalLifecycle>();
        services.AddScoped<TerminalSession>();

        return services;
    }
}
