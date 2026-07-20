using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ApplicationDependencyInjectionTests : IDisposable
{
    private readonly string _backupDirectory = Path.Combine(
        Path.GetTempPath(), $"UnityAssetsPatcher-{Guid.NewGuid():N}");

    [Fact]
    public void BuildServiceProvider_WithCompleteObjectGraph_ValidatesScopesAndBuild()
    {
        using ServiceProvider provider = CreateServices(new StubAssetsFileService([]))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<IWorkflowService>());
    }

    [Fact]
    public void BuildServiceProvider_WithoutAssetsAccessScopeFactory_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddUnityAssetsPatcherApplication(_backupDirectory);

        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
    }

    [Fact]
    public void AssetCalls_CreateAndDisposeOneIndependentAccessScopePerCall()
    {
        StubAssetsFileService assetsFileService = CreateAssetsFileService();
        using ServiceProvider provider = CreateServices(assetsFileService).BuildServiceProvider();
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        workflows.InspectFields(new InspectFieldsRequest("first.assets", 1));
        workflows.InspectFields(new InspectFieldsRequest("second.assets", 2));

        Assert.Equal(2, assetsFileService.ScopeCreateCount);
        Assert.Equal(2, assetsFileService.ReaderCreateCount);
        Assert.Equal(2, assetsFileService.WriterCreateCount);
        Assert.Equal(2, assetsFileService.ScopeDisposeCount);
        Assert.Equal(2, assetsFileService.ReaderDisposeCount);
        Assert.Equal(2, assetsFileService.WriterDisposeCount);
    }

    [Fact]
    public void AssetServices_InOneDependencyInjectionScope_ShareOneLazyAccessScope()
    {
        StubAssetsFileService assetsFileService = CreateAssetsFileService();
        using ServiceProvider provider = CreateServices(assetsFileService).BuildServiceProvider();

        using (IServiceScope dependencyInjectionScope = provider.CreateScope())
        {
            IAssetsAccessScope firstAccess = dependencyInjectionScope.ServiceProvider
                .GetRequiredService<IAssetsAccessScope>();
            IAssetsAccessScope secondAccess = dependencyInjectionScope.ServiceProvider
                .GetRequiredService<IAssetsAccessScope>();
            IAssetsFileReader firstReader = dependencyInjectionScope.ServiceProvider
                .GetRequiredService<IAssetsFileReader>();
            IAssetsFileReader secondReader = dependencyInjectionScope.ServiceProvider
                .GetRequiredService<IAssetsFileReader>();
            IAssetsFileWriter firstWriter = dependencyInjectionScope.ServiceProvider
                .GetRequiredService<IAssetsFileWriter>();
            IAssetsFileWriter secondWriter = dependencyInjectionScope.ServiceProvider
                .GetRequiredService<IAssetsFileWriter>();

            Assert.Same(firstAccess, secondAccess);
            Assert.Same(firstReader, secondReader);
            Assert.Same(firstWriter, secondWriter);
            Assert.Equal(0, assetsFileService.ScopeCreateCount);

            firstReader.ReadAssets("first.assets");

            Assert.Equal(1, assetsFileService.ScopeCreateCount);
            Assert.Equal(0, assetsFileService.ScopeDisposeCount);
        }

        Assert.Equal(1, assetsFileService.ScopeDisposeCount);
        Assert.Equal(1, assetsFileService.ReaderDisposeCount);
        Assert.Equal(1, assetsFileService.WriterDisposeCount);
    }

    [Fact]
    public void AssetCall_WhenReaderThrows_DisposesReaderWriterAndScopeOnce()
    {
        var assetsFileService = new StubAssetsFileService([]);
        using ServiceProvider provider = CreateServices(assetsFileService).BuildServiceProvider();
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        Assert.Throws<InvalidOperationException>(() =>
            workflows.InspectFields(new InspectFieldsRequest("broken.assets", 1)));

        Assert.Equal(1, assetsFileService.ScopeCreateCount);
        Assert.Equal(1, assetsFileService.ReaderCreateCount);
        Assert.Equal(1, assetsFileService.WriterCreateCount);
        Assert.Equal(1, assetsFileService.ScopeDisposeCount);
        Assert.Equal(1, assetsFileService.ReaderDisposeCount);
        Assert.Equal(1, assetsFileService.WriterDisposeCount);
    }

    [Fact]
    public void NonAssetCalls_DoNotCreateAssetsAccessScope()
    {
        Directory.CreateDirectory(_backupDirectory);
        string manifestPath = Path.Combine(_backupDirectory, "manifest.json");
        File.WriteAllText(manifestPath, TestManifest.CreateJson(
            """
            {
              "target": "sharedassets0.assets",
              "type": "Camera",
              "match": { "m_Name": "Old" },
              "set": { "m_Name": { "from": "Old", "to": "New" } }
            }
            """));
        var assetsFileService = new StubAssetsFileService([]);
        using ServiceProvider provider = CreateServices(assetsFileService).BuildServiceProvider();
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        workflows.CheckManifest(manifestPath);
        workflows.ListInstalledMods();
        workflows.CheckPendingTransactions();
        workflows.RecoverPendingTransactions(_backupDirectory);

        Assert.Equal(0, assetsFileService.ScopeCreateCount);
        Assert.Equal(0, assetsFileService.ReaderCreateCount);
        Assert.Equal(0, assetsFileService.WriterCreateCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_backupDirectory))
        {
            Directory.Delete(_backupDirectory, true);
        }
    }

    private ServiceCollection CreateServices(StubAssetsFileService assetsFileService)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAssetsAccessScopeFactory>(assetsFileService);
        services.AddUnityAssetsPatcherApplication(_backupDirectory);

        return services;
    }

    private static StubAssetsFileService CreateAssetsFileService()
    {
        var firstField = new AssetField("Base", "Base", null, []);
        var secondField = new AssetField("Base", "Base", null, []);

        return new StubAssetsFileService(
            [],
            new Dictionary<long, AssetField>
            {
                [1] = firstField,
                [2] = secondField,
            });
    }
}
