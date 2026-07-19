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
        using ServiceProvider provider = CreateServices(new RecordingAssetsFileServices())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<IWorkflowService>());
    }

    [Fact]
    public void BuildServiceProvider_WithoutAssetsFileServices_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddUnityAssetsPatcherApplication(_backupDirectory);

        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
    }

    [Fact]
    public void AssetCalls_CreateAndDisposeOneIndependentReaderPerCall()
    {
        var assetsFileServices = new RecordingAssetsFileServices();
        using ServiceProvider provider = CreateServices(assetsFileServices).BuildServiceProvider();
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        workflows.InspectFields(new InspectFieldsRequest("first.assets", 1));
        workflows.InspectFields(new InspectFieldsRequest("second.assets", 2));

        Assert.Equal(2, assetsFileServices.Readers.Count);
        Assert.All(assetsFileServices.Readers, reader => Assert.Equal(1, reader.DisposeCount));
        Assert.NotSame(assetsFileServices.Readers[0], assetsFileServices.Readers[1]);
        Assert.Empty(assetsFileServices.Writers);
    }

    [Fact]
    public void AssetServices_InOneDependencyInjectionScope_CreateOneReaderAndWriter()
    {
        var assetsFileServices = new RecordingAssetsFileServices();
        using ServiceProvider provider = CreateServices(assetsFileServices).BuildServiceProvider();

        using (IServiceScope dependencyInjectionScope = provider.CreateScope())
        {
            dependencyInjectionScope.ServiceProvider.GetRequiredService<IAssetsFileReader>();
            dependencyInjectionScope.ServiceProvider.GetRequiredService<IAssetsFileReader>();
            dependencyInjectionScope.ServiceProvider.GetRequiredService<IAssetsFileWriter>();
            dependencyInjectionScope.ServiceProvider.GetRequiredService<IAssetsFileWriter>();

            Assert.Single(assetsFileServices.Readers);
            Assert.Single(assetsFileServices.Writers);
            Assert.Equal(0, assetsFileServices.Readers[0].DisposeCount);
            Assert.Equal(0, assetsFileServices.Writers[0].DisposeCount);
        }

        Assert.Equal(1, assetsFileServices.Readers[0].DisposeCount);
        Assert.Equal(1, assetsFileServices.Writers[0].DisposeCount);
    }

    [Fact]
    public void AssetCall_WhenReaderThrows_DisposesReaderOnce()
    {
        var assetsFileServices = new RecordingAssetsFileServices(throwOnRead: true);
        using ServiceProvider provider = CreateServices(assetsFileServices).BuildServiceProvider();
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        Assert.Throws<InvalidOperationException>(() =>
            workflows.InspectFields(new InspectFieldsRequest("broken.assets", 1)));

        RecordingAssetsFileReader reader = Assert.Single(assetsFileServices.Readers);
        Assert.Equal(1, reader.DisposeCount);
        Assert.Empty(assetsFileServices.Writers);
    }

    [Fact]
    public void BackupCall_DoesNotCreateAssetsFileAccess()
    {
        var assetsFileServices = new RecordingAssetsFileServices();
        using ServiceProvider provider = CreateServices(assetsFileServices).BuildServiceProvider();

        provider.GetRequiredService<IWorkflowService>().CheckPendingTransactions();

        Assert.Empty(assetsFileServices.Readers);
        Assert.Empty(assetsFileServices.Writers);
    }

    public void Dispose()
    {
        if (Directory.Exists(_backupDirectory))
        {
            Directory.Delete(_backupDirectory, true);
        }
    }

    private ServiceCollection CreateServices(RecordingAssetsFileServices assetsFileServices)
    {
        var services = new ServiceCollection();
        services.AddScoped<IAssetsFileReader>(_ => assetsFileServices.CreateReader());
        services.AddScoped<IAssetsFileWriter>(_ => assetsFileServices.CreateWriter());
        services.AddUnityAssetsPatcherApplication(_backupDirectory);

        return services;
    }

    private sealed class RecordingAssetsFileServices(bool throwOnRead = false)
    {
        public List<RecordingAssetsFileReader> Readers { get; } = [];
        public List<RecordingAssetsFileWriter> Writers { get; } = [];

        public IAssetsFileReader CreateReader()
        {
            var reader = new RecordingAssetsFileReader(throwOnRead);
            Readers.Add(reader);

            return reader;
        }

        public IAssetsFileWriter CreateWriter()
        {
            var writer = new RecordingAssetsFileWriter();
            Writers.Add(writer);

            return writer;
        }
    }

    private sealed class RecordingAssetsFileReader(bool throwOnRead) : IAssetsFileReader
    {
        public int DisposeCount { get; private set; }

        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            return [];
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            if (throwOnRead)
            {
                throw new InvalidOperationException("Test read failure.");
            }

            return new AssetField("Base", "Base", null, []);
        }

        public void CloseReadSessions() { }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class RecordingAssetsFileWriter : IAssetsFileWriter
    {
        public int DisposeCount { get; private set; }

        public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan) { }

        public void WriteReplacements(
            string inputPath,
            string outputPath,
            IReadOnlyList<AssetReplacement> plan) { }

        public void WriteFieldPatchesAndCopies(
            string inputPath,
            string outputPath,
            IReadOnlyList<AssetFieldPatch> fieldPatches,
            IReadOnlyList<AssetCopy> copies) { }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
