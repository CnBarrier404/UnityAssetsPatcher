using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Repository;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Repository;

public sealed class FileRepositoryStoreTests
{
    [Fact]
    public void ClearUnsupportedRepository_WhenFormatIsUnsupported_ReplacesOnlyRepositoryContents()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string outsidePath = directory.WriteFile("outside.txt", "outside");
        string legacyPath = directory.WriteFile("backup/installed/legacy/record.json", "legacy");
        directory.WriteFile("backup/repository.json", "{\"formatVersion\":1}");
        FileRepositoryLayout layout = new(repositoryPath);
        FileRepositoryStore store = CreateStore(layout);

        RepositoryClearResult result;
        using (IRepositoryOperationLock operationLock = new FileRepositoryOperationLockProvider(layout).Acquire())
        {
            result = store.ClearUnsupportedRepository(operationLock);

            Assert.True(File.Exists(layout.LockPath));
            Assert.True(File.Exists(layout.MetadataPath));
        }

        Assert.Equal(1, result.PreviousFormatVersion);
        Assert.Equal(FileCatalogStore.CurrentRepositoryFormatVersion, result.FormatVersion);
        Assert.False(File.Exists(legacyPath));
        Assert.False(Directory.Exists(Path.Combine(repositoryPath, "installed")));
        Assert.False(File.Exists(layout.LockPath));
        Assert.Equal("outside", File.ReadAllText(outsidePath));
        RepositoryMetadata metadata = store.LoadOrCreateMetadata();
        Assert.Equal(result.FormatVersion, metadata.FormatVersion);
        Assert.Matches("^[0-9a-f]{32}$", metadata.RepositoryId);
    }

    [Fact]
    public void ClearUnsupportedRepository_WhenFormatIsCurrent_RejectsWithoutDeletingContents()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        FileRepositoryLayout layout = new(repositoryPath);
        FileRepositoryStore store = CreateStore(layout);
        RepositoryMetadata metadata = store.LoadOrCreateMetadata();
        string markerPath = directory.WriteFile("backup/marker.txt", "keep");

        using IRepositoryOperationLock operationLock = new FileRepositoryOperationLockProvider(layout).Acquire();

        Assert.Throws<RepositoryClearNotAllowedException>(() => store.ClearUnsupportedRepository(operationLock));
        Assert.Equal("keep", File.ReadAllText(markerPath));
        Assert.Equal(metadata, store.LoadOrCreateMetadata());
    }

    [Fact]
    public void ClearUnsupportedRepository_WhenLockBelongsToAnotherRepository_RejectsWithoutDeletingContents()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string otherRepositoryPath = directory.CreateDirectory("other-backup");
        string metadataPath = directory.WriteFile("backup/repository.json", "{\"formatVersion\":1}");
        FileRepositoryStore store = CreateStore(new FileRepositoryLayout(repositoryPath));
        using IRepositoryOperationLock operationLock =
            new FileRepositoryOperationLockProvider(new FileRepositoryLayout(otherRepositoryPath)).Acquire();

        Assert.Throws<InvalidOperationException>(() => store.ClearUnsupportedRepository(operationLock));
        Assert.True(File.Exists(metadataPath));
    }

    private static FileRepositoryStore CreateStore(FileRepositoryLayout layout)
    {
        IFileSystemOperations fileSystem = new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);

        return new FileRepositoryStore(layout, fileSystem, NullLoggerFactory.Instance);
    }
}
