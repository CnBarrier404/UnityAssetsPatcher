using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class FileRepository : IRepositoryStorage, ICompositionRepository
{
    public string RepositoryDirectory => _layout.RepositoryDirectory;
    public string TransactionDirectory => _layout.TransactionDirectory;
    public IBaseSnapshotStore BaseSnapshots => _baseSnapshotStore;
    public ILayerStore Layers => _layerStore;

    private readonly FileRepositoryLayout _layout;
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

        _layout = new FileRepositoryLayout(repositoryDirectory);
        RepositoryFileSystem repositoryFileSystem = new(fileSystemOperations);
        RepositoryJsonPersistence jsonPersistence = new(fileSystemOperations);
        _catalogStore = new FileCatalogStore(
            _layout,
            fileSystemOperations,
            repositoryFileSystem,
            jsonPersistence,
            loggerFactory.CreateLogger<FileCatalogStore>());
        _baseSnapshotStore = new BaseSnapshotStore(
            _layout,
            fileSystemOperations,
            repositoryFileSystem,
            jsonPersistence);
        _layerStore = new LayerStore(
            _layout,
            fileSystemOperations,
            repositoryFileSystem,
            jsonPersistence);
    }

    public RepositoryMetadata LoadOrCreateMetadata()
    {
        return _catalogStore.LoadOrCreateMetadata();
    }
}
