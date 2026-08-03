using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Backups;

public interface IBackupRepository
{
    public string RepositoryDirectory { get; }

    public string InstalledDirectory { get; }

    public string TransactionDirectory { get; }

    public BackupRepositoryMetadata LoadOrCreateMetadata();

    public string GetInstallDirectory(string installId);

    public InstallRecordEntry ReadRecord(string installId);

    public IReadOnlyList<InstallRecordEntry> ListRecords();

    public FileIntegrity StoreVerifiedCopy(
        string sourcePath,
        string preparedInstallDirectory,
        string backupRelativePath);

    public string ResolveBackupPath(string installDirectory, string backupRelativePath);

    public void WritePreparedRecord(InstallRecord record, string preparedInstallDirectory);

    public void CommitInstall(string preparedInstallDirectory, string installId);
}
