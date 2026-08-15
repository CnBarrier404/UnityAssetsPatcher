using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Infrastructure.Repository;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Repository;

public sealed class FileCatalogStoreTests
{
    [Fact]
    public void LoadOrCreateMetadata_WhenRepositoryIsMissing_CreatesStableVersionTwoLayout()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        FileCatalogStore store = CreateCatalog(repositoryPath);

        RepositoryMetadata created = store.LoadOrCreateMetadata();
        RepositoryMetadata loaded = CreateCatalog(repositoryPath).LoadOrCreateMetadata();

        Assert.Equal(FileCatalogStore.CurrentRepositoryFormatVersion, created.FormatVersion);
        Assert.Matches("^[0-9a-f]{32}$", created.RepositoryId);
        Assert.Equal(created, loaded);
        Assert.True(File.Exists(Path.Combine(repositoryPath, FileCatalogStore.RepositoryFileName)));
        Assert.False(Directory.Exists(store.TransactionDirectory));
    }

    [Fact]
    public void LoadOrCreateMetadata_WhenRepositoryUsesVersionOne_RejectsRepository()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        directory.WriteFile(
            "backup/repository.json",
            "{\"formatVersion\":1,\"repositoryId\":\"repository\"}");
        FileCatalogStore store = CreateCatalog(repositoryPath);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(store.LoadOrCreateMetadata);

        Assert.Equal("Unsupported backup repository format: 1.", exception.Message);
    }

    [Fact]
    public void LoadOrCreateMetadata_WhenFormatVersionIsUnknown_RejectsRepository()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        directory.WriteFile("backup/repository.json", "{\"formatVersion\":3,\"repositoryId\":\"repository\"}");
        FileCatalogStore store = CreateCatalog(repositoryPath);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(store.LoadOrCreateMetadata);

        Assert.Equal("Unsupported backup repository format: 3.", exception.Message);
    }

    [Fact]
    public void CreateFingerprint_WhenDirectoryIsResolved_ReturnsStableSha256()
    {
        using RepositoryTestDirectory directory = new();
        FileSystemOperations fileSystem = CreateFileSystem();
        var resolver = new TrustedPathResolver(fileSystem);

        string first = GameInstanceIdentity.CreateFingerprint(resolver, directory.Path);
        string second = GameInstanceIdentity.CreateFingerprint(resolver, directory.Path);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    private static FileCatalogStore CreateCatalog(
        string repositoryPath,
        FileSystemOperations? fileSystem = null)
    {
        return new FileCatalogStore(
            repositoryPath,
            fileSystem ?? CreateFileSystem(),
            NullLogger<FileCatalogStore>.Instance);
    }

    private static FileSystemOperations CreateFileSystem()
    {
        return new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
    }
}
