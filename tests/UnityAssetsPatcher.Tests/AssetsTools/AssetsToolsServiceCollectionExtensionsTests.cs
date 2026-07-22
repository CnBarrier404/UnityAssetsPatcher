using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Infrastructure;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsToolsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUnityAssetsPatcherAssetsTools_FactoryScopeReaderReturnsAssetData()
    {
        using ServiceProvider provider = CreateServiceProvider();
        IAssetsAccessScopeFactory factory = provider.GetRequiredService<IAssetsAccessScopeFactory>();
        using IAssetsAccessScope scope = factory.CreateScope();

        IReadOnlyList<AssetInfo> assets = scope.Reader.ReadAssets(GetRealAssetsFilePath());

        Assert.NotEmpty(assets);
    }

    [Fact]
    public void AddUnityAssetsPatcherAssetsTools_RegistersSingletonFactoryAndContext()
    {
        using ServiceProvider provider = CreateServiceProvider();

        IAssetsAccessScopeFactory firstFactory = provider.GetRequiredService<IAssetsAccessScopeFactory>();
        IAssetsAccessScopeFactory secondFactory = provider.GetRequiredService<IAssetsAccessScopeFactory>();
        AssetsToolsContext firstContext = provider.GetRequiredService<AssetsToolsContext>();
        AssetsToolsContext secondContext = provider.GetRequiredService<AssetsToolsContext>();

        Assert.Same(firstFactory, secondFactory);
        Assert.Same(firstContext, secondContext);
    }

    [Fact]
    public void AddUnityAssetsPatcherAssetsTools_WithApplication_ValidatesCompleteObjectGraph()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddUnityAssetsPatcherInfrastructure()
            .AddUnityAssetsPatcherAssetsTools(() => File.OpenRead(GetRealTpkFilePath()))
            .AddUnityAssetsPatcherApplication("backup")
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        Assert.NotNull(provider.GetRequiredService<IAssetsAccessScopeFactory>());
    }

    [Fact]
    public void CreateScope_WhenCalledTwice_ReturnsIndependentReadersAndWriters()
    {
        using ServiceProvider provider = CreateServiceProvider();
        IAssetsAccessScopeFactory factory = provider.GetRequiredService<IAssetsAccessScopeFactory>();
        using IAssetsAccessScope firstScope = factory.CreateScope();
        using IAssetsAccessScope secondScope = factory.CreateScope();

        Assert.NotSame(firstScope, secondScope);
        Assert.NotSame(firstScope.Reader, secondScope.Reader);
        Assert.NotSame(firstScope.Writer, secondScope.Writer);
        Assert.IsType<AssetsFileReader>(firstScope.Reader);
        Assert.IsType<AssetsFileWriter>(firstScope.Writer);
    }

    [Fact]
    public void DisposeScope_DisposesReader()
    {
        using ServiceProvider provider = CreateServiceProvider();
        IAssetsAccessScope scope = provider.GetRequiredService<IAssetsAccessScopeFactory>().CreateScope();
        IAssetsFileReader reader = scope.Reader;

        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => reader.ReadAssets(GetRealAssetsFilePath()));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddUnityAssetsPatcherInfrastructure()
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
