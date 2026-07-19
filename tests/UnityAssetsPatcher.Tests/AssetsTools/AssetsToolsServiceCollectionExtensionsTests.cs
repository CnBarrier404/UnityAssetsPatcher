using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsToolsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUnityAssetsPatcherAssetsTools_ReaderReturnsAssetData()
    {
        using ServiceProvider provider = CreateServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IAssetsFileReader reader = scope.ServiceProvider.GetRequiredService<IAssetsFileReader>();

        IReadOnlyList<AssetInfo> assets = reader.ReadAssets(GetRealAssetsFilePath());

        Assert.NotEmpty(assets);
    }

    [Fact]
    public void AddUnityAssetsPatcherAssetsTools_RegistersScopedReaderAndWriter()
    {
        using ServiceProvider provider = CreateServiceProvider();
        using IServiceScope firstScope = provider.CreateScope();
        IAssetsFileReader firstReader = firstScope.ServiceProvider.GetRequiredService<IAssetsFileReader>();
        IAssetsFileWriter firstWriter = firstScope.ServiceProvider.GetRequiredService<IAssetsFileWriter>();

        Assert.Same(firstReader, firstScope.ServiceProvider.GetRequiredService<IAssetsFileReader>());
        Assert.Same(firstWriter, firstScope.ServiceProvider.GetRequiredService<IAssetsFileWriter>());

        using IServiceScope secondScope = provider.CreateScope();

        Assert.NotSame(firstReader, secondScope.ServiceProvider.GetRequiredService<IAssetsFileReader>());
        Assert.NotSame(firstWriter, secondScope.ServiceProvider.GetRequiredService<IAssetsFileWriter>());
        Assert.IsType<AssetsFileReader>(firstReader);
        Assert.IsType<AssetsFileWriter>(firstWriter);
    }

    [Fact]
    public void DisposeScope_DisposesReader()
    {
        using ServiceProvider provider = CreateServiceProvider();
        IServiceScope scope = provider.CreateScope();
        IAssetsFileReader reader = scope.ServiceProvider.GetRequiredService<IAssetsFileReader>();

        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => reader.ReadAssets(GetRealAssetsFilePath()));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddUnityAssetsPatcherAssetsTools(() => File.OpenRead(GetRealTpkFilePath()))
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
    }

    private static string FindRepositoryRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "UnityAssetsPatcher.slnx")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string GetRealAssetsFilePath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "UnityAssetsPatcher.Tests",
            "RealTestAssets",
            "sharedassets0.assets");
    }

    private static string GetRealTpkFilePath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Assets",
            "resources.tpk");
    }
}
