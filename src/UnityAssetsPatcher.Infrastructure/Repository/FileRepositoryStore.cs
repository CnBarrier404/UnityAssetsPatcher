using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class FileRepositoryStore : IRepositoryStore
{
    public string RepositoryDirectory => _layout.RepositoryDirectory;
    public string TransactionDirectory => _layout.TransactionDirectory;
    public IBaseSnapshotStore BaseSnapshots => _baseSnapshotStore;
    public ILayerStore Layers => _layerStore;
    public IRepositoryTransactionStore Transactions => _transactionStore;

    private readonly FileRepositoryLayout _layout;
    private readonly RepositoryFileSystem _repositoryFileSystem;
    private readonly FileCatalogStore _catalogStore;
    private readonly BaseSnapshotStore _baseSnapshotStore;
    private readonly LayerStore _layerStore;
    private readonly FileRepositoryTransactionStore _transactionStore;

    public FileRepositoryStore(
        FileRepositoryLayout layout,
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _layout = layout;
        _repositoryFileSystem = new RepositoryFileSystem(fileSystemOperations);
        RepositoryJsonPersistence jsonPersistence = new(fileSystemOperations);
        _catalogStore = new FileCatalogStore(
            _layout,
            fileSystemOperations,
            _repositoryFileSystem,
            jsonPersistence,
            loggerFactory.CreateLogger<FileCatalogStore>());
        _baseSnapshotStore = new BaseSnapshotStore(
            _layout,
            fileSystemOperations,
            _repositoryFileSystem,
            jsonPersistence);
        _layerStore = new LayerStore(
            _layout,
            fileSystemOperations,
            _repositoryFileSystem,
            jsonPersistence);
        _transactionStore = new FileRepositoryTransactionStore(
            _layout,
            fileSystemOperations,
            _repositoryFileSystem,
            jsonPersistence);
    }

    public RepositoryMetadata LoadOrCreateMetadata()
    {
        return _catalogStore.LoadOrCreateMetadata();
    }

    public RepositoryClearResult ClearUnsupportedRepository(IRepositoryOperationLock operationLock)
    {
        ArgumentNullException.ThrowIfNull(operationLock);
        operationLock.EnsureHeldFor(RepositoryDirectory);

        UnsupportedRepositoryFormatException unsupportedFormat;
        try
        {
            _ = _catalogStore.LoadOrCreateMetadata();
            throw new RepositoryClearNotAllowedException();
        }
        catch (UnsupportedRepositoryFormatException exception)
        {
            unsupportedFormat = exception;
        }

        _repositoryFileSystem.ClearRepositoryDirectory(
            RepositoryDirectory,
            _layout.MetadataPath,
            _layout.LockPath);

        RepositoryMetadata metadata = _catalogStore.LoadOrCreateMetadata();

        return new RepositoryClearResult(unsupportedFormat.ActualVersion, metadata.FormatVersion);
    }
}
