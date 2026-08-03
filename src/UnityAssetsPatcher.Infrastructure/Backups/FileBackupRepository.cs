using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Backups;

internal sealed class FileBackupRepository : IBackupRepository
{
    public string RepositoryDirectory => _catalogStore.RepositoryDirectory;

    public string InstalledDirectory => _catalogStore.InstalledDirectory;

    public string TransactionDirectory => _catalogStore.TransactionDirectory;

    private readonly FileBackupCatalogStore _catalogStore;
    private readonly FileBackupStore _fileStore;

    public FileBackupRepository(
        string repositoryDirectory,
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory loggerFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _catalogStore = new FileBackupCatalogStore(
            repositoryDirectory,
            fileSystemOperations,
            loggerFactory.CreateLogger<FileBackupCatalogStore>());
        _fileStore = new FileBackupStore(
            repositoryDirectory,
            fileSystemOperations,
            loggerFactory.CreateLogger<FileBackupStore>());
    }

    public BackupRepositoryMetadata LoadOrCreateMetadata()
    {
        return _catalogStore.LoadOrCreateMetadata();
    }

    public string GetInstallDirectory(string installId)
    {
        return _catalogStore.GetInstallDirectory(installId);
    }

    public InstallRecordEntry ReadRecord(string installId)
    {
        return _catalogStore.ReadRecord(installId);
    }

    public IReadOnlyList<InstallRecordEntry> ListRecords()
    {
        return _catalogStore.ListRecords();
    }

    public FileIntegrity StoreVerifiedCopy(
        string sourcePath,
        string preparedInstallDirectory,
        string backupRelativePath)
    {
        return _fileStore.StoreVerifiedCopy(sourcePath, preparedInstallDirectory, backupRelativePath);
    }

    public string ResolveBackupPath(string installDirectory, string backupRelativePath)
    {
        return _fileStore.ResolveBackupPath(installDirectory, backupRelativePath);
    }

    public void WritePreparedRecord(InstallRecord record, string preparedInstallDirectory)
    {
        _catalogStore.WritePreparedRecord(record, preparedInstallDirectory);
    }

    public void CommitInstall(string preparedInstallDirectory, string installId)
    {
        _catalogStore.CommitInstall(preparedInstallDirectory, installId);
    }
}
