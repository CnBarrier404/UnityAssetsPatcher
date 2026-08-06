using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class FileRepository : IRepository, ICompositionRepository
{
    public string RepositoryDirectory => _catalogStore.RepositoryDirectory;

    public string TransactionDirectory => _catalogStore.TransactionDirectory;

    public IBaseSnapshotStore BaseSnapshots => _baseSnapshotStore;

    public ILayerStore Layers => _layerStore;

    private readonly FileCatalogStore _catalogStore;
    private readonly BaseSnapshotStore _baseSnapshotStore;
    private readonly LayerStore _layerStore;

    public FileRepository(
        string repositoryDirectory,
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory loggerFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _catalogStore = new FileCatalogStore(
            repositoryDirectory,
            fileSystemOperations,
            loggerFactory.CreateLogger<FileCatalogStore>());
        _baseSnapshotStore = new BaseSnapshotStore(repositoryDirectory, fileSystemOperations);
        _layerStore = new LayerStore(repositoryDirectory, fileSystemOperations);
    }

    public RepositoryMetadata LoadOrCreateMetadata()
    {
        return _catalogStore.LoadOrCreateMetadata();
    }

    public string GetLegacyInstallDirectory(string installId)
    {
        return _catalogStore.GetLegacyInstallDirectory(installId);
    }

    public LegacyInstallRecordEntry ReadLegacyRecord(string installId)
    {
        return _catalogStore.ReadLegacyRecord(installId);
    }

    public IReadOnlyList<LegacyInstallRecordEntry> ListLegacyRecords()
    {
        return _catalogStore.ListLegacyRecords();
    }
}
