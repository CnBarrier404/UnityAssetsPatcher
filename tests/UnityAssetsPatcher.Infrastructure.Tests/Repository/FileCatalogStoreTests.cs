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
        Assert.False(Directory.Exists(Path.Combine(repositoryPath, FileCatalogStore.InstalledDirectoryName)));
        Assert.False(Directory.Exists(store.TransactionDirectory));
    }

    [Fact]
    public void ListRecords_WhenRepositoryContainsVersionOneFixture_PreservesLegacyRecord()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = InitializeFixtureRepository(directory);
        FileCatalogStore store = CreateCatalog(repositoryPath);

        LegacyInstallRecordEntry entry = Assert.Single(store.ListLegacyRecords());
        LegacyInstallRecord record = entry.Record;

        Assert.Equal("compat-repository-v1", record.RepositoryId);
        Assert.Equal("committed-install-v1", record.Id);
        Assert.Equal(7, record.InstallSequence);
        Assert.Equal(DateTimeOffset.Parse("2025-06-15T12:34:56+00:00"), record.InstalledAt);
        Assert.Equal("Compatibility Mod", record.ModName);
        Assert.Equal("1.2.3", record.ModVersion);
        Assert.Equal("Compatibility Author", record.ModAuthor);
        Assert.Equal("Compatibility Game", record.GameName);
        Assert.Equal(["HD Textures", "Extra Audio"], record.OptionalGroups);
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
    public void ListLegacyRecords_WhenRepositoryUsesVersionTwo_ReturnsNoLegacyRecords()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        FileCatalogStore catalog = CreateCatalog(repositoryPath);
        RepositoryMetadata metadata = catalog.LoadOrCreateMetadata();

        Assert.Equal(FileCatalogStore.CurrentRepositoryFormatVersion, metadata.FormatVersion);
        Assert.Empty(catalog.ListLegacyRecords());
    }

    [Fact]
    public void ListLegacyRecords_WhenFixtureContainsTraversal_RejectsRepository()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = InitializeFixtureRepository(directory);
        string recordPath = Path.Combine(
            repositoryPath,
            FileCatalogStore.InstalledDirectoryName,
            "committed-install-v1",
            FileCatalogStore.RecordFileName);
        string json = File.ReadAllText(recordPath)
            .Replace("Game_Data/sharedassets0.assets", "../outside.assets", StringComparison.Ordinal);

        File.WriteAllText(recordPath, json);

        FileCatalogStore store = CreateCatalog(repositoryPath);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => store.ListLegacyRecords());

        Assert.Contains("path is not trusted", exception.Message);
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

    private static string InitializeFixtureRepository(RepositoryTestDirectory directory)
    {
        string repositoryPath = directory.CreateDirectory("backup");
        string installDirectory = directory.CreateDirectory(
            "backup",
            FileCatalogStore.InstalledDirectoryName,
            "committed-install-v1");

        File.Copy(
            FixturePath("repository-v1.json"),
            Path.Combine(repositoryPath, FileCatalogStore.RepositoryFileName));

        File.Copy(
            FixturePath("install-record-v1.json"),
            Path.Combine(installDirectory, FileCatalogStore.RecordFileName));

        return repositoryPath;
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

    private static string FixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Compatibility", "Fixtures", fileName);
    }
}
