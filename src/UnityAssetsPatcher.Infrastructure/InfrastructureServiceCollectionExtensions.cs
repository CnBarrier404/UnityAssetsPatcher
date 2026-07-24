using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Abstractions.IO;
using UnityAssetsPatcher.Infrastructure.IO;

namespace UnityAssetsPatcher.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICompressionCodec, BrotliCompression>();
        services.AddSingleton<IFileSystemOperations, FileSystemOperations>();

        return services;
    }
}
