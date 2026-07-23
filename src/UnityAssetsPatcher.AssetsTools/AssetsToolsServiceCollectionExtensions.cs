using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.AssetsTools;

public static class AssetsToolsServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherAssetsTools(
        this IServiceCollection services,
        Func<Stream> openTpkStream)
    {
        ArgumentNullException.ThrowIfNull(openTpkStream);
        services.AddSingleton<IAssetsAccessScopeFactory>(provider => new AssetsToolsAccessScopeFactory(
            openTpkStream,
            provider.GetRequiredService<IFileOperations>(),
            provider.GetRequiredService<IDirectoryOperations>()));

        return services;
    }
}
