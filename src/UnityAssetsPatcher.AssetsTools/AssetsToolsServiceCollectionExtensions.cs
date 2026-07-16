using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public static class AssetsToolsServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherAssetsTools(
        this IServiceCollection services,
        Func<Stream> openTpkStream)
    {
        services.AddSingleton<IAssetsAccessScopeFactory>(_ => new AssetsToolsAccessScopeFactory(openTpkStream));

        return services;
    }
}
