using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public static class AssetsToolsServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherAssetsTools(
        this IServiceCollection services,
        Func<Stream> openTpkStream)
    {
        services.AddSingleton(_ => new AssetsToolsContext(openTpkStream));
        services.AddSingleton<IAssetsAccessScopeFactory, AssetsToolsAccessScopeFactory>();

        return services;
    }
}
