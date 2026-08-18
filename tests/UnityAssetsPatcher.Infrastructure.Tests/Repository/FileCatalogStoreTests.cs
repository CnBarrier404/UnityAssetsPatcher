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
        FileRepositoryLayout layout = new(repositoryPath);
        FileCatalogStore store = CreateCatalog(layout);

        RepositoryMetadata created = store.LoadOrCreateMetadata();
        RepositoryMetadata loaded = CreateCatalog(new FileRepositoryLayout(repositoryPath)).LoadOrCreateMetadata();

        Assert.Equal(FileCatalogStore.CurrentRepositoryFormatVersion, created.FormatVersion);
        Assert.Matches("^[0-9a-f]{32}$", created.RepositoryId);
        Assert.Equal(created, loaded);
        Assert.True(File.Exists(layout.MetadataPath));
        Assert.False(Directory.Exists(layout.TransactionDirectory));
    }

    [Fact]
    public void LoadOrCreateMetadata_WhenRepositoryUsesVersionOne_RejectsRepository()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        directory.WriteFile(
            "backup/repository.json",
            "{\"formatVersion\":1,\"repositoryId\":\"repository\"}");
        FileCatalogStore store = CreateCatalog(new FileRepositoryLayout(repositoryPath));

        var exception = Assert.Throws<NotSupportedException>(store.LoadOrCreateMetadata);

        Assert.Equal("Unsupported backup repository format: 1.", exception.Message);
    }

    [Fact]
    public void LoadOrCreateMetadata_WhenFormatVersionIsUnknown_RejectsRepository()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        directory.WriteFile("backup/repository.json", "{\"formatVersion\":3,\"repositoryId\":\"repository\"}");
        FileCatalogStore store = CreateCatalog(new FileRepositoryLayout(repositoryPath));

        var exception = Assert.Throws<NotSupportedException>(store.LoadOrCreateMetadata);

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
        FileRepositoryLayout layout,
        FileSystemOperations? fileSystem = null)
    {
        FileSystemOperations operations = fileSystem ?? CreateFileSystem();

        return new FileCatalogStore(
            layout,
            operations,
            new RepositoryFileSystem(operations),
            new RepositoryJsonPersistence(operations),
            NullLogger<FileCatalogStore>.Instance);
    }

    private static FileSystemOperations CreateFileSystem()
    {
        return new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
    }
}
