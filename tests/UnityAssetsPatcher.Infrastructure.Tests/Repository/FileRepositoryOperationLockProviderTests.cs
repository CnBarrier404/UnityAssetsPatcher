using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Infrastructure.Repository;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Repository;

public sealed class FileRepositoryOperationLockProviderTests
{
    [Fact]
    public void Acquire_WhenLockIsAlreadyHeld_ThrowsExpectedException()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        FileRepositoryOperationLockProvider provider = CreateProvider(repositoryPath);
        using IRepositoryOperationLock operationLock = provider.Acquire();

        _ = Assert.Throws<IOException>(provider.Acquire);
    }

    [Fact]
    public void Dispose_DeletesLockFileAndAllowsAnotherAcquire()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        FileRepositoryLayout layout = new(repositoryPath);
        FileRepositoryOperationLockProvider provider = new(layout);
        IRepositoryOperationLock firstLock = provider.Acquire();

        Assert.Equal(Path.Combine(repositoryPath, ".lock"), layout.LockPath);
        Assert.True(File.Exists(layout.LockPath));

        firstLock.Dispose();

        Assert.False(File.Exists(layout.LockPath));
        using IRepositoryOperationLock secondLock = provider.Acquire();
        Assert.True(File.Exists(layout.LockPath));
    }

    [Fact]
    public void EnsureHeldFor_WhenLockIsDisposed_ThrowsExpectedException()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        IRepositoryOperationLock operationLock = CreateProvider(repositoryPath).Acquire();
        operationLock.Dispose();

        var exception = Assert.Throws<InvalidOperationException>(() => operationLock.EnsureHeldFor(repositoryPath));

        Assert.Equal("The backup operation lock is no longer held.", exception.Message);
    }

    [Fact]
    public void EnsureHeldFor_WhenLockBelongsToAnotherRepository_ThrowsExpectedException()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string otherRepositoryPath = directory.CreateDirectory("other-backup");
        using IRepositoryOperationLock operationLock = CreateProvider(repositoryPath).Acquire();

        var exception =
            Assert.Throws<InvalidOperationException>(() => operationLock.EnsureHeldFor(otherRepositoryPath));

        Assert.Equal("The backup operation lock belongs to another repository.", exception.Message);
    }

    private static FileRepositoryOperationLockProvider CreateProvider(string repositoryPath)
    {
        return new FileRepositoryOperationLockProvider(new FileRepositoryLayout(repositoryPath));
    }
}
