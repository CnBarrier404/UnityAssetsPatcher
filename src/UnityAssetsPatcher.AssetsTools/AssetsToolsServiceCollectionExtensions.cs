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
        services.AddScoped<IAssetsFileReader>(provider => new AssetsFileReader(
            provider.GetRequiredService<AssetsToolsContext>(),
            ownsContext: false));
        services.AddScoped<IAssetsFileWriter>(provider => new AssetsFileWriter(
            provider.GetRequiredService<AssetsToolsContext>()));

        return services;
    }
}
