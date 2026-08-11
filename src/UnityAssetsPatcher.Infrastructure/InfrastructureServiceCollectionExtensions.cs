using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Infrastructure.AssetsTools;
using UnityAssetsPatcher.Infrastructure.Repository;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Installation;
using UnityAssetsPatcher.Infrastructure.Mods;
using UnityAssetsPatcher.Infrastructure.Updates;

namespace UnityAssetsPatcher.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherUpdateChecking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<IUpdateChecker, GitHubUpdateChecker>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    public static IServiceCollection AddUnityAssetsPatcherInfrastructure(
        this IServiceCollection services,
        Func<Stream> openClassPackage)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(openClassPackage);

        services.TryAddSingleton<IFileSystemOperations>(provider => new FileSystemOperations(
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<FileSystemOperations>()));
        services.TryAddSingleton<IModPackageReader, ZipModPackageReader>();

        services.TryAddSingleton<SteamInstallationOptions>(_ => SteamInstallationOptions.FromCurrentMachine());
        services.TryAddSingleton<IGameInstallationLocator, SteamGameInstallationLocator>();

        services.TryAddSingleton<ICompressionCodec>(provider => new BrotliCompression(
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<BrotliCompression>()));

        services.TryAddSingleton<IAssetFileSessionFactory>(provider => new AssetsToolsAssetFileSessionFactory(
            openClassPackage,
            provider.GetRequiredService<IFileSystemOperations>(),
            provider.GetRequiredService<ILoggerFactory>()));

        services.TryAddSingleton<IAssetsAccessScopeFactory, AssetFileAccessScopeFactory>();
        services.TryAddScoped(provider => provider.GetRequiredService<IAssetsAccessScopeFactory>().CreateScope());
        services.TryAddScoped(provider => provider.GetRequiredService<IAssetsAccessScope>().Reader);
        services.TryAddScoped(provider => provider.GetRequiredService<IAssetsAccessScope>().Writer);

        return services;
    }

    public static IServiceCollection AddUnityAssetsPatcherRepository(
        this IServiceCollection services,
        string repositoryDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);

        services.TryAddSingleton<IFileSystemOperations>(provider => new FileSystemOperations(
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<FileSystemOperations>()));

        services.TryAddSingleton<FileRepository>(provider => new FileRepository(
            repositoryDirectory,
            provider.GetRequiredService<IFileSystemOperations>(),
            provider.GetRequiredService<ILoggerFactory>()));
        services.TryAddSingleton<IRepositoryStorage>(provider => provider.GetRequiredService<FileRepository>());
        services.TryAddSingleton<ICompositionRepository>(provider => provider.GetRequiredService<FileRepository>());

        return services;
    }
}
