using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Backups;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Backups;

public sealed class FileBackupCatalogStoreTests
{
    [Fact]
    public void LoadOrCreateMetadata_WhenRepositoryIsMissing_CreatesStableVersionOneLayout()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        FileBackupCatalogStore store = CreateCatalog(repositoryPath);

        BackupRepositoryMetadata created = store.LoadOrCreateMetadata();
        BackupRepositoryMetadata loaded = CreateCatalog(repositoryPath).LoadOrCreateMetadata();

        Assert.Equal(FileBackupCatalogStore.CurrentRepositoryFormatVersion, created.FormatVersion);
        Assert.Matches("^[0-9a-f]{32}$", created.RepositoryId);
        Assert.Equal(created, loaded);
        Assert.True(File.Exists(Path.Combine(repositoryPath, FileBackupCatalogStore.RepositoryFileName)));
        Assert.True(Directory.Exists(store.InstalledDirectory));
        Assert.False(Directory.Exists(store.TransactionDirectory));
    }

    [Fact]
    public void ListRecords_WhenRepositoryContainsVersionOneFixture_PreservesLegacyRecord()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = InitializeFixtureRepository(directory);
        FileBackupCatalogStore store = CreateCatalog(repositoryPath);

        InstallRecordEntry entry = Assert.Single(store.ListRecords());
        InstallRecord record = entry.Record;

        Assert.Equal("compat-repository-v1", record.RepositoryId);
        Assert.Equal("committed-install-v1", record.Id);
        Assert.Equal(7, record.InstallSequence);
        Assert.Equal(DateTimeOffset.Parse("2025-06-15T12:34:56+00:00"), record.InstalledAt);
        Assert.Equal("Compatibility Mod", record.ModName);
        Assert.Equal("1.2.3", record.ModVersion);
        Assert.Equal("Compatibility Author", record.ModAuthor);
        Assert.Equal("Compatibility Game", record.GameName);
        Assert.Equal(["HD Textures", "Extra Audio"], record.OptionalGroups);

        InstallRecordPatchedFile patchedFile = Assert.Single(record.PatchedFiles);
        Assert.Equal("sharedassets0.assets", patchedFile.Target);
        Assert.Equal("Game_Data/sharedassets0.assets", patchedFile.AssetsFileRelativePath);
        Assert.Equal("original/sharedassets0.assets", patchedFile.BackupRelativePath);
        Assert.Equal(2, patchedFile.AssetCount);
        Assert.Equal(3, patchedFile.OperationCount);
        Assert.Equal(16, patchedFile.InstalledFile.Length);
        Assert.Equal(
            "5a8f3e0443cebca98d109935efaad7ceedd1a18d710e1cd496dbd367981411f8",
            patchedFile.InstalledFile.Sha256);

        InstallRecordCopiedFile copiedFile = Assert.Single(record.CopiedFiles);
        Assert.Equal("payload/mod.bin", copiedFile.Source);
        Assert.Equal("Game_Data/mod.bin", copiedFile.DestinationRelativePath);
        Assert.Equal(7, copiedFile.InstalledFile.Length);
    }

    [Fact]
    public void WritePreparedRecord_WhenLegacyFixtureIsLoaded_ProducesEquivalentVersionOneJson()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = InitializeFixtureRepository(directory);
        FileBackupCatalogStore store = CreateCatalog(repositoryPath);
        InstallRecord record = Assert.Single(store.ListRecords()).Record;
        Directory.Delete(store.GetInstallDirectory(record.Id), recursive: true);
        string preparedDirectory = directory.CreateDirectory(
            "backup",
            FileBackupCatalogStore.TransactionDirectoryName,
            "prepared-install");

        store.WritePreparedRecord(record, preparedDirectory);

        JsonNode? expected = JsonNode.Parse(File.ReadAllText(FixturePath("install-record-v1.json")));
        JsonNode? actual = JsonNode.Parse(File.ReadAllText(
            Path.Combine(preparedDirectory, FileBackupCatalogStore.RecordFileName)));

        Assert.True(JsonNode.DeepEquals(expected, actual));
    }

    [Fact]
    public void LoadOrCreateMetadata_WhenFormatVersionIsUnknown_RejectsRepository()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        directory.WriteFile("backup/repository.json", "{\"formatVersion\":2,\"repositoryId\":\"repository\"}");
        FileBackupCatalogStore store = CreateCatalog(repositoryPath);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(store.LoadOrCreateMetadata);

        Assert.Equal("Unsupported backup repository format: 2.", exception.Message);
    }

    [Fact]
    public void WritePreparedRecord_WhenOptionalGroupsAreAbsent_OmitsLegacyJsonProperty()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        FileBackupCatalogStore catalog = CreateCatalog(repositoryPath);
        BackupRepositoryMetadata metadata = catalog.LoadOrCreateMetadata();
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "prepared-install");
        InstallRecord record = CreateRecord(metadata.RepositoryId, "install-1", EmptyIntegrity);

        catalog.WritePreparedRecord(record, preparedDirectory);

        var document = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(
            Path.Combine(preparedDirectory, FileBackupCatalogStore.RecordFileName))));

        Assert.False(document.ContainsKey("optionalGroups"));
        Assert.True(document.ContainsKey("gameName"));
    }

    [Fact]
    public void ListRecords_WhenFixtureContainsTraversal_RejectsRepository()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = InitializeFixtureRepository(directory);
        string recordPath = Path.Combine(
            repositoryPath,
            FileBackupCatalogStore.InstalledDirectoryName,
            "committed-install-v1",
            FileBackupCatalogStore.RecordFileName);
        string json = File.ReadAllText(recordPath)
            .Replace("Game_Data/sharedassets0.assets", "../outside.assets", StringComparison.Ordinal);

        File.WriteAllText(recordPath, json);

        FileBackupCatalogStore store = CreateCatalog(repositoryPath);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => store.ListRecords());

        Assert.Contains("path is not trusted", exception.Message);
    }

    [Fact]
    public void WriteAndCommitInstall_WhenPreparationIsValid_MovesRecordAndVerifiedBackupTogether()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile("game/sharedassets0.assets", "original");
        FileSystemOperations fileSystem = CreateFileSystem();
        FileBackupRepository repository = CreateRepository(repositoryPath, fileSystem);
        BackupRepositoryMetadata metadata = repository.LoadOrCreateMetadata();
        string preparedDirectory = directory.CreateDirectory(
            "backup",
            FileBackupCatalogStore.TransactionDirectoryName,
            "prepared-install");

        FileIntegrity backupIntegrity = repository.StoreVerifiedCopy(
            sourcePath,
            preparedDirectory,
            "backups/assets-0.bin");
        InstallRecord record = CreateRecord(metadata.RepositoryId, "install-1", backupIntegrity);

        repository.WritePreparedRecord(record, preparedDirectory);

        repository.CommitInstall(preparedDirectory, record.Id);

        InstallRecordEntry installed = repository.ReadRecord(record.Id);
        string backupPath = repository.ResolveBackupPath(installed.InstallDirectory, "backups/assets-0.bin");

        Assert.Equivalent(record, installed.Record, strict: true);
        Assert.Equal("original", File.ReadAllText(backupPath));
        Assert.False(Directory.Exists(preparedDirectory));
    }

    [Fact]
    public void CommitInstall_WhenPreparedBackupWasModified_RejectsCommit()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile("game/sharedassets0.assets", "original");
        FileSystemOperations fileSystem = CreateFileSystem();
        FileBackupCatalogStore catalog = CreateCatalog(repositoryPath, fileSystem);
        FileBackupStore backups = CreateBackupStore(repositoryPath, fileSystem);
        BackupRepositoryMetadata metadata = catalog.LoadOrCreateMetadata();
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "prepared-install");
        FileIntegrity backupIntegrity = backups.StoreVerifiedCopy(
            sourcePath,
            preparedDirectory,
            "backups/assets-0.bin");
        InstallRecord record = CreateRecord(metadata.RepositoryId, "install-1", backupIntegrity);

        catalog.WritePreparedRecord(record, preparedDirectory);

        File.WriteAllText(Path.Combine(preparedDirectory, "backups", "assets-0.bin"), "modified");

        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(() => catalog.CommitInstall(preparedDirectory, record.Id));

        Assert.Contains("integrity does not match", exception.Message);
        Assert.True(Directory.Exists(preparedDirectory));
        Assert.False(Directory.Exists(catalog.GetInstallDirectory(record.Id)));
    }

    [Fact]
    public void WritePreparedRecord_WhenRepositoryIdDoesNotMatch_DoesNotWriteRecord()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        FileBackupCatalogStore catalog = CreateCatalog(repositoryPath);
        _ = catalog.LoadOrCreateMetadata();
        string preparedDirectory = directory.CreateDirectory(
            "backup",
            FileBackupCatalogStore.TransactionDirectoryName,
            "prepared-install");
        InstallRecord record = CreateRecord("other-repository", "install-1", EmptyIntegrity);

        _ = Assert.Throws<InvalidDataException>(() => catalog.WritePreparedRecord(record, preparedDirectory));

        Assert.False(File.Exists(Path.Combine(preparedDirectory, FileBackupCatalogStore.RecordFileName)));
    }

    [Fact]
    public void WritePreparedRecord_WhenGameSequenceIsDuplicated_RejectsRecordBeforeCommit()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        FileBackupCatalogStore catalog = CreateCatalog(repositoryPath);
        BackupRepositoryMetadata metadata = catalog.LoadOrCreateMetadata();
        InstallRecord first = CreateRecord(metadata.RepositoryId, "install-1", EmptyIntegrity, sequence: 1);
        InstallRecord second = CreateRecord(metadata.RepositoryId, "install-2", EmptyIntegrity, sequence: 1);

        string firstPreparation = directory.CreateDirectory("backup", ".temp", "first");

        directory.WriteFile("backup/.temp/first/backups/assets-0.bin", string.Empty);

        catalog.WritePreparedRecord(first, firstPreparation);

        catalog.CommitInstall(firstPreparation, first.Id);

        string secondPreparation = directory.CreateDirectory("backup", ".temp", "second");

        _ = Assert.Throws<InvalidDataException>(() => catalog.WritePreparedRecord(second, secondPreparation));

        Assert.False(File.Exists(Path.Combine(secondPreparation, FileBackupCatalogStore.RecordFileName)));
    }

    [Fact]
    public void CreateFingerprint_WhenDirectoryIsResolved_ReturnsStableSha256()
    {
        using BackupTestDirectory directory = new();
        FileSystemOperations fileSystem = CreateFileSystem();
        var resolver = new TrustedPathResolver(fileSystem);

        string first = GameInstanceIdentity.CreateFingerprint(resolver, directory.Path);
        string second = GameInstanceIdentity.CreateFingerprint(resolver, directory.Path);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    private static string InitializeFixtureRepository(BackupTestDirectory directory)
    {
        string repositoryPath = directory.CreateDirectory("backup");
        string installDirectory = directory.CreateDirectory(
            "backup",
            FileBackupCatalogStore.InstalledDirectoryName,
            "committed-install-v1");

        File.Copy(
            FixturePath("repository-v1.json"),
            Path.Combine(repositoryPath, FileBackupCatalogStore.RepositoryFileName));

        File.Copy(
            FixturePath("install-record-v1.json"),
            Path.Combine(installDirectory, FileBackupCatalogStore.RecordFileName));

        return repositoryPath;
    }

    private static InstallRecord CreateRecord(
        string repositoryId,
        string installId,
        FileIntegrity backupIntegrity,
        long sequence = 1)
    {
        return new InstallRecord(
            repositoryId,
            new string('0', FileIntegrity.Sha256HexLength),
            sequence,
            installId,
            DateTimeOffset.UnixEpoch,
            "Test Mod",
            "1.0.0",
            "tests",
            "Test Game",
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    "Game_Data/sharedassets0.assets",
                    "backups/assets-0.bin",
                    1,
                    1,
                    backupIntegrity,
                    backupIntegrity),
            ],
            []);
    }

    private static FileBackupCatalogStore CreateCatalog(
        string repositoryPath,
        FileSystemOperations? fileSystem = null)
    {
        return new FileBackupCatalogStore(
            repositoryPath,
            fileSystem ?? CreateFileSystem(),
            NullLogger<FileBackupCatalogStore>.Instance);
    }

    private static FileBackupStore CreateBackupStore(string repositoryPath, FileSystemOperations fileSystem)
    {
        return new FileBackupStore(
            repositoryPath,
            fileSystem,
            NullLogger<FileBackupStore>.Instance);
    }

    private static FileBackupRepository CreateRepository(
        string repositoryPath,
        FileSystemOperations fileSystem)
    {
        return new FileBackupRepository(
            repositoryPath,
            fileSystem,
            NullLoggerFactory.Instance);
    }

    private static FileSystemOperations CreateFileSystem()
    {
        return new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
    }

    private static string FixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Compatibility", "Fixtures", fileName);
    }

    private static FileIntegrity EmptyIntegrity { get; } = new(
        0,
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
}
