using Microsoft.Extensions.DependencyInjection;

namespace UnityAssetsPatcher.TUI;

public static class TerminalServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherTUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TerminalSettings>();
        services.AddSingleton<TerminalApp>();

        return services;
    }
}
