using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Infrastructure.IO;

namespace UnityAssetsPatcher.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFileOperations, FileOperations>();
        services.AddSingleton<IDirectoryOperations, DirectoryOperations>();

        return services;
    }
}
