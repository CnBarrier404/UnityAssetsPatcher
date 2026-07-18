using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ApplicationDependencyInjectionTests : IDisposable
{
    private readonly string _backupDirectory = Path.Combine(
        Path.GetTempPath(), $"UnityAssetsPatcher-{Guid.NewGuid():N}");

    [Fact]
    public void BuildServiceProvider_WithCompleteObjectGraph_ValidatesScopesAndBuild()
    {
        using ServiceProvider provider = CreateServices(new RecordingAssetsAccessScopeFactory())
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
    public void AssetCalls_CreateAndDisposeOneIndependentScopePerCall()
    {
        var factory = new RecordingAssetsAccessScopeFactory();
        using ServiceProvider provider = CreateServices(factory).BuildServiceProvider();
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        workflows.InspectFields(new InspectFieldsRequest("first.assets", 1));
        workflows.InspectFields(new InspectFieldsRequest("second.assets", 2));

        Assert.Equal(2, factory.Scopes.Count);
        Assert.All(factory.Scopes, scope => Assert.Equal(1, scope.DisposeCount));
        Assert.NotSame(factory.Scopes[0], factory.Scopes[1]);
    }

    [Fact]
    public void AssetServices_InOneDependencyInjectionScope_ShareOneUnderlyingScope()
    {
        var factory = new RecordingAssetsAccessScopeFactory();
        using ServiceProvider provider = CreateServices(factory).BuildServiceProvider();

        using (IServiceScope dependencyInjectionScope = provider.CreateScope())
        {
            dependencyInjectionScope.ServiceProvider.GetRequiredService<IAssetsFileReader>();
            dependencyInjectionScope.ServiceProvider.GetRequiredService<IAssetsFileWriter>();

            Assert.Single(factory.Scopes);
            Assert.Equal(0, factory.Scopes[0].DisposeCount);
        }

        Assert.Equal(1, factory.Scopes[0].DisposeCount);
    }

    [Fact]
    public void AssetCall_WhenReaderThrows_DisposesScopeOnce()
    {
        var factory = new RecordingAssetsAccessScopeFactory(throwOnRead: true);
        using ServiceProvider provider = CreateServices(factory).BuildServiceProvider();
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        Assert.Throws<InvalidOperationException>(() =>
            workflows.InspectFields(new InspectFieldsRequest("broken.assets", 1)));

        RecordingAssetsAccessScope scope = Assert.Single(factory.Scopes);
        Assert.Equal(1, scope.DisposeCount);
    }

    [Fact]
    public void BackupCall_DoesNotCreateAssetsScope()
    {
        var factory = new RecordingAssetsAccessScopeFactory();
        using ServiceProvider provider = CreateServices(factory).BuildServiceProvider();

        provider.GetRequiredService<IWorkflowService>().CheckPendingTransactions();

        Assert.Empty(factory.Scopes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_backupDirectory)) Directory.Delete(_backupDirectory, true);
    }

    private ServiceCollection CreateServices(IAssetsAccessScopeFactory factory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(factory);
        services.AddSingleton<IAssetsAccessScopeFactory>(factory);
        services.AddUnityAssetsPatcherApplication(_backupDirectory);
        return services;
    }

    private sealed class RecordingAssetsAccessScopeFactory(bool throwOnRead = false) : IAssetsAccessScopeFactory
    {
        public List<RecordingAssetsAccessScope> Scopes { get; } = [];

        public IAssetsAccessScope CreateScope()
        {
            var scope = new RecordingAssetsAccessScope(throwOnRead);
            Scopes.Add(scope);
            return scope;
        }
    }

    private sealed class RecordingAssetsAccessScope(bool throwOnRead) :
        IAssetsAccessScope, IAssetsFileReader, IAssetsFileWriter
    {
        public int DisposeCount { get; private set; }
        public IAssetsFileReader Reader => this;
        public IAssetsFileWriter Writer => this;

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath) => [];

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            if (throwOnRead) throw new InvalidOperationException("Test read failure.");
            return new AssetsFieldInfo("Base", "Base", null, []);
        }

        public void CloseReadSessions() { }
        public void WritePatch(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan) { }
        public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan) { }
        public void Dispose() => DisposeCount++;
    }
}
