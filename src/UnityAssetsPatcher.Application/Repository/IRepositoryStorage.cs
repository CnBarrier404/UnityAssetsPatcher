namespace UnityAssetsPatcher.Application.Repository;

public interface IRepositoryStorage
{
    public string RepositoryDirectory { get; }

    public string TransactionDirectory { get; }

    public RepositoryMetadata LoadOrCreateMetadata();
}
