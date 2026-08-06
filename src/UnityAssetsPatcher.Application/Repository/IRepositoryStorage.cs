namespace UnityAssetsPatcher.Application.Repository;

public interface IRepositoryStorage
{
    public string RepositoryDirectory { get; }

    public string TransactionDirectory { get; }

    public RepositoryMetadata LoadOrCreateMetadata();

    public string GetLegacyInstallDirectory(string installId);

    public LegacyInstallRecordEntry ReadLegacyRecord(string installId);

    public IReadOnlyList<LegacyInstallRecordEntry> ListLegacyRecords();
}
