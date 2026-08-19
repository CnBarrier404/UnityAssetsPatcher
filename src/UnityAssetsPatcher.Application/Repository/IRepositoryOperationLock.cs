namespace UnityAssetsPatcher.Application.Repository;

public interface IRepositoryOperationLock : IDisposable
{
    public void EnsureHeldFor(string repositoryDirectory);
}

public interface IRepositoryOperationLockProvider
{
    public IRepositoryOperationLock Acquire();
}
