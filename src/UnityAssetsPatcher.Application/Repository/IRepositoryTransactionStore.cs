namespace UnityAssetsPatcher.Application.Repository;

public interface IRepositoryTransactionStore
{
    public RepositoryTransaction? TryLoad();

    public void Save(RepositoryTransaction transaction);

    public void Delete();
}
