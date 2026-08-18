using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Repository;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Repository;

public sealed class CompositionStoreTests
{
    [Fact]
    public void BaseSnapshotStore_WhenCatalogAndFileAreStored_RoundTripsAndVerifiesIntegrity()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile(@"game\Game_Data\sharedassets0.assets", "base-assets");
        FileSystemOperations fileSystem = CreateFileSystem();
        BaseSnapshotStore store = CreateBaseSnapshotStore(repositoryPath, fileSystem);
        FileIntegrity integrity = fileSystem.ComputeFileIntegrity(sourcePath);

        FileIntegrity stored = store.StoreVerifiedCopy(
            "game-fingerprint",
            "Game_Data/sharedassets0.assets",
            sourcePath);
        BaseCatalog catalog = new(
            "game-fingerprint",
            DateTimeOffset.UnixEpoch,
            [new BaseFileEntry("Game_Data/sharedassets0.assets", integrity)],
            [new PayloadBaseEntry("Data/config.txt", PayloadBaseState.Absent)]);

        store.WriteCatalog(catalog);

        BaseCatalog? loaded = store.TryReadCatalog("game-fingerprint");
        FileIntegrity verified = store.VerifyFile(
            "game-fingerprint",
            "Game_Data/sharedassets0.assets",
            integrity);

        Assert.NotNull(loaded);

        Assert.Equal(integrity, stored);
        Assert.Equal(catalog.GameInstanceFingerprint, loaded!.GameInstanceFingerprint);
        Assert.Equal(catalog.CapturedAt, loaded.CapturedAt);
        Assert.Equal(catalog.AssetsFiles, loaded.AssetsFiles);
        Assert.Equal(catalog.PayloadTargets, loaded.PayloadTargets);
        Assert.Equal(integrity, verified);
        Assert.Equal("base-assets", File.ReadAllText(store.ResolveFilePath(
            "game-fingerprint",
            "Game_Data/sharedassets0.assets")));
        Assert.True(File.Exists(Path.Combine(repositoryPath, "games", "game-fingerprint", "base", "catalog.json")));
    }

    [Fact]
    public void BaseSnapshotStore_WhenCatalogJsonIsCorrupt_ThrowsInvalidDataException()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        BaseSnapshotStore store = CreateBaseSnapshotStore(repositoryPath, CreateFileSystem());

        _ = store.GetBaseDirectory("game-fingerprint");
        Directory.CreateDirectory(Path.Combine(repositoryPath, "games", "game-fingerprint", "base"));
        File.WriteAllText(
            Path.Combine(repositoryPath, "games", "game-fingerprint", "base", "catalog.json"),
            "{not-json");

        var exception =
            Assert.Throws<InvalidDataException>(() => store.ReadCatalog("game-fingerprint"));

        Assert.Contains("contains invalid", exception.Message);
    }

    [Fact]
    public void BaseSnapshotStore_WhenStoredFileIsModified_RejectsVerification()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile(@"game\base.assets", "original");
        FileSystemOperations fileSystem = CreateFileSystem();
        BaseSnapshotStore store = CreateBaseSnapshotStore(repositoryPath, fileSystem);
        FileIntegrity integrity = store.StoreVerifiedCopy("game-fingerprint", "base.assets", sourcePath);
        string storedPath = store.ResolveFilePath("game-fingerprint", "base.assets");

        File.WriteAllText(storedPath, "modified");

        var exception = Assert.Throws<InvalidDataException>(() => store.VerifyFile(
            "game-fingerprint",
            "base.assets",
            integrity));

        Assert.Contains("integrity does not match", exception.Message);
    }

    [Fact]
    public void BaseSnapshotStore_WhenExistingSnapshotDiffers_RejectsOverwrite()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile("source.bin", "original");
        FileSystemOperations fileSystem = CreateFileSystem();
        BaseSnapshotStore store = CreateBaseSnapshotStore(repositoryPath, fileSystem);

        FileIntegrity first = store.StoreVerifiedCopy("game-fingerprint", "base.assets", sourcePath);
        File.WriteAllText(sourcePath, "changed");

        var exception = Assert.Throws<InvalidDataException>(() => store.StoreVerifiedCopy(
            "game-fingerprint",
            "base.assets",
            sourcePath));

        Assert.Contains("already exists with different integrity", exception.Message);
        Assert.Equal(first, fileSystem.ComputeFileIntegrity(store.ResolveFilePath("game-fingerprint", "base.assets")));
        Assert.Equal("original", File.ReadAllText(store.ResolveFilePath("game-fingerprint", "base.assets")));
    }

    [Theory]
    [InlineData("../outside.assets")]
    [InlineData("C:/outside.assets")]
    [InlineData("\\\\server\\share\\outside.assets")]
    public void BaseSnapshotStore_WhenRelativePathIsUnsafe_RejectsPath(string relativePath)
    {
        using RepositoryTestDirectory directory = new();
        BaseSnapshotStore store = CreateBaseSnapshotStore(directory.GetPath("backup"), CreateFileSystem());
        string sourcePath = directory.WriteFile("source.bin", "source");

        var exception = Assert.Throws<IOException>(() => store.StoreVerifiedCopy(
            "game-fingerprint",
            relativePath,
            sourcePath));

        Assert.Contains("relative path", exception.Message);
    }

    [Fact]
    public void LayerStore_WhenLayerAndPackageAreCommitted_RoundTripsAndVerifiesIntegrity()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile("source.zip", "package-content");
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "layer-1");
        FileSystemOperations fileSystem = CreateFileSystem();
        LayerStore store = CreateLayerStore(repositoryPath, fileSystem);
        FileIntegrity integrity = fileSystem.ComputeFileIntegrity(sourcePath);
        LayerRecord record = CreateLayerRecord(integrity);

        FileIntegrity stored = store.StoreVerifiedPackage(
            sourcePath,
            preparedDirectory,
            record.Package);
        store.WritePreparedLayer(record, preparedDirectory);
        store.CommitLayer(preparedDirectory, record.Id);

        LayerRecordEntry loaded = store.ReadLayer(record.Id);
        var layers = store.ListLayers();
        FileIntegrity verified = store.VerifyPackage(record.Id);
        string packagePath = store.ResolvePackagePath(record.Id);

        Assert.Equal(integrity, stored);
        Assert.Equivalent(record, loaded.Record, true);
        Assert.Equivalent(loaded.Record, Assert.Single(layers).Record, true);
        Assert.Equal(integrity, verified);
        Assert.Equal("package-content", File.ReadAllText(packagePath));
        Assert.True(File.Exists(Path.Combine(repositoryPath, "layers", record.Id, "layer.json")));
        Assert.False(Directory.Exists(preparedDirectory));
    }

    [Fact]
    public void LayerStore_WhenLayerJsonIsCorrupt_ThrowsInvalidDataException()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string layerDirectory = directory.CreateDirectory("backup", "layers", "layer-1");
        File.WriteAllText(Path.Combine(layerDirectory, "layer.json"), "{not-json");
        LayerStore store = CreateLayerStore(repositoryPath, CreateFileSystem());

        var exception = Assert.Throws<InvalidDataException>(() => store.ReadLayer("layer-1"));

        Assert.Contains("contains invalid", exception.Message);
    }

    [Fact]
    public void LayerStore_WhenPackageIsModified_RejectsVerification()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile("source.zip", "package-content");
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "layer-1");
        FileSystemOperations fileSystem = CreateFileSystem();
        LayerStore store = CreateLayerStore(repositoryPath, fileSystem);
        LayerRecord record = CreateLayerRecord(fileSystem.ComputeFileIntegrity(sourcePath));

        store.StoreVerifiedPackage(sourcePath, preparedDirectory, record.Package);
        store.WritePreparedLayer(record, preparedDirectory);
        store.CommitLayer(preparedDirectory, record.Id);
        File.WriteAllText(store.ResolvePackagePath(record.Id), "modified");

        var exception = Assert.Throws<InvalidDataException>(() => store.VerifyPackage(record.Id));

        Assert.Contains("integrity does not match", exception.Message);
    }

    [Fact]
    public void LayerStore_WhenLayerIsDeleted_RemovesLayerDirectory()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile("source.zip", "package-content");
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "layer-1");
        FileSystemOperations fileSystem = CreateFileSystem();
        LayerStore store = CreateLayerStore(repositoryPath, fileSystem);
        LayerRecord record = CreateLayerRecord(fileSystem.ComputeFileIntegrity(sourcePath));

        store.StoreVerifiedPackage(sourcePath, preparedDirectory, record.Package);
        store.WritePreparedLayer(record, preparedDirectory);
        store.CommitLayer(preparedDirectory, record.Id);

        store.DeleteLayer(record.Id);

        Assert.False(Directory.Exists(store.GetLayerDirectory(record.Id)));
        Assert.Empty(store.ListLayers());
    }

    [Fact]
    public void LayerStore_WhenPreparedDirectoryIsOutsideTransaction_RejectsPath()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string sourcePath = directory.WriteFile("source.zip", "package-content");
        string outsideDirectory = directory.CreateDirectory("outside");
        directory.CreateDirectory("backup", ".temp");
        LayerStore store = CreateLayerStore(repositoryPath, CreateFileSystem());
        LayerRecord record = CreateLayerRecord(FileIntegrity.Create("package-content"u8));

        var exception = Assert.Throws<InvalidOperationException>(() => store.StoreVerifiedPackage(
            sourcePath,
            outsideDirectory,
            record.Package));

        Assert.Contains("outside the active transaction", exception.Message);
    }

    [Fact]
    public void LayerStore_WhenLayerJsonContainsUnsafePackagePath_RejectsLayer()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.GetPath("backup");
        string layerDirectory = directory.CreateDirectory("backup", "layers", "layer-1");
        File.WriteAllText(
            Path.Combine(layerDirectory, "layer.json"),
            """
            {
              "repositoryId": "repository",
              "gameInstanceFingerprint": "game-fingerprint",
              "installSequence": 1,
              "id": "layer-1",
              "installedAt": "1970-01-01T00:00:00+00:00",
              "modName": "Test Mod",
              "modVersion": "1.0.0",
              "modAuthor": "Test Author",
              "gameName": "Test Game",
              "optionalGroups": null,
              "enabled": true,
              "package": {
                "fileName": "../package.zip",
                "integrity": {
                  "length": 15,
                  "sha256": "2e7d2c03a9507ae265ecf5b5356885a53393a202d9d241394997265a1a25aefc"
                }
              },
              "assetsTargets": [],
              "payloadTargets": []
            }
            """);
        LayerStore store = CreateLayerStore(repositoryPath, CreateFileSystem());

        var exception = Assert.Throws<InvalidDataException>(() => store.ReadLayer("layer-1"));

        Assert.Contains("invalid data", exception.Message);
    }

    [Fact]
    public void AddRepository_WhenCompositionRepositoryIsResolved_UsesRepositoryInstance()
    {
        using RepositoryTestDirectory directory = new();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddUnityAssetsPatcherRepository(directory.GetPath("backup"));

        using ServiceProvider provider = services.BuildServiceProvider();
        var repositoryStorage = provider.GetRequiredService<IRepositoryStorage>();
        var compositionRepository = provider.GetRequiredService<ICompositionRepository>();

        Assert.Same(repositoryStorage, compositionRepository);
        Assert.NotNull(compositionRepository.BaseSnapshots);
        Assert.NotNull(compositionRepository.Layers);
    }

    private static LayerRecord CreateLayerRecord(FileIntegrity packageIntegrity)
    {
        return new LayerRecord(
            "repository",
            "game-fingerprint",
            1,
            "layer-1",
            DateTimeOffset.UnixEpoch,
            "Test Mod",
            "1.0.0",
            "Test Author",
            "Test Game",
            ["hd-textures"],
            true,
            new LayerPackageInfo(FileRepositoryLayout.PackageFileName, packageIntegrity),
            ["Game_Data/sharedassets0.assets"],
            ["Data/config.txt"]);
    }

    private static FileSystemOperations CreateFileSystem()
    {
        return new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
    }

    private static BaseSnapshotStore CreateBaseSnapshotStore(
        string repositoryPath,
        FileSystemOperations fileSystem)
    {
        return new BaseSnapshotStore(
            new FileRepositoryLayout(repositoryPath),
            fileSystem,
            new RepositoryFileSystem(fileSystem),
            new RepositoryJsonPersistence(fileSystem));
    }

    private static LayerStore CreateLayerStore(
        string repositoryPath,
        FileSystemOperations fileSystem)
    {
        return new LayerStore(
            new FileRepositoryLayout(repositoryPath),
            fileSystem,
            new RepositoryFileSystem(fileSystem),
            new RepositoryJsonPersistence(fileSystem));
    }
}
