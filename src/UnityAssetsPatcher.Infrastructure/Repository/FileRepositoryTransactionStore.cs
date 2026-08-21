using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class FileRepositoryTransactionStore : IRepositoryTransactionStore
{
    private readonly FileRepositoryLayout _layout;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly RepositoryFileSystem _repositoryFileSystem;
    private readonly RepositoryJsonPersistence _jsonPersistence;

    public FileRepositoryTransactionStore(
        FileRepositoryLayout layout,
        IFileSystemOperations fileSystemOperations,
        RepositoryFileSystem repositoryFileSystem,
        RepositoryJsonPersistence jsonPersistence)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(repositoryFileSystem);
        ArgumentNullException.ThrowIfNull(jsonPersistence);

        _layout = layout;
        _fileSystemOperations = fileSystemOperations;
        _repositoryFileSystem = repositoryFileSystem;
        _jsonPersistence = jsonPersistence;
    }

    public RepositoryTransaction? TryLoad()
    {
        if (!_repositoryFileSystem.TryGetAttributes(_layout.TransactionDirectory, out _))
        {
            return null;
        }

        _repositoryFileSystem.EnsureRealDirectory(_layout.TransactionDirectory, "Transaction directory");
        _repositoryFileSystem.EnsureRegularFile(_layout.TransactionPath, "Transaction");

        return _jsonPersistence.Read(
            _layout.TransactionPath,
            RepositoryJsonContext.Default.RepositoryTransaction,
            "Transaction");
    }

    public void Save(RepositoryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        _fileSystemOperations.EnsureDirectory(_layout.TransactionDirectory);
        _jsonPersistence.Write(
            _layout.TransactionPath,
            transaction,
            RepositoryJsonContext.Default.RepositoryTransaction,
            FileDestinationMode.CreateOrReplace);
    }

    public void Delete()
    {
        _repositoryFileSystem.EnsureRealDirectory(_layout.TransactionDirectory, "Transaction directory");
        _fileSystemOperations.DeleteDirectoryTree(_layout.TransactionDirectory);
    }
}
