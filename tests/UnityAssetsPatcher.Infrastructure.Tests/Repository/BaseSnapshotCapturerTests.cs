using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Repository;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Repository;

public sealed class BaseSnapshotCapturerTests
{
    [Fact]
    public void Capture_WhenAssetsFileIsNew_CapturesAndRegistersBaseEntry()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string gameDirectory = directory.CreateDirectory("game");
        string relativePath = Path.Combine("Game_Data", "sharedassets0.assets");
        string sourcePath = directory.WriteFile(Path.Combine("game", relativePath), "original-assets");
        FileSystemOperations fileSystem = CreateFileSystem();
        FileRepository repository = CreateRepository(repositoryPath, fileSystem);
        BaseSnapshotCapturer capturer = new(repository, fileSystem);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(fileSystem, gameDirectory);

        using RepositoryOperationLock operationLock = AcquireLock(repositoryPath);
        BaseCatalog catalog = capturer.Capture(
            operationLock,
            gameDirectory,
            relativePath,
            RepositoryFileKind.Assets);

        BaseFileEntry entry = Assert.Single(catalog.AssetsFiles);
        string snapshotPath = repository.BaseSnapshots.ResolveFilePath(fingerprint, relativePath);

        Assert.Equal(relativePath, entry.RelativePath);
        Assert.Equal(fileSystem.ComputeFileIntegrity(sourcePath), entry.Integrity);
        Assert.Equal("original-assets", File.ReadAllText(snapshotPath));
        Assert.Equal(entry, capturer.TryGetAssetsEntry(gameDirectory, relativePath));
    }

    [Fact]
    public void Capture_WhenPayloadFileExists_CapturesPresentEntry()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string gameDirectory = directory.CreateDirectory("game");
        string relativePath = Path.Combine("BepInEx", "plugins", "example.dll");
        string sourcePath = directory.WriteFile(Path.Combine("game", relativePath), "payload");
        FileSystemOperations fileSystem = CreateFileSystem();
        FileRepository repository = CreateRepository(repositoryPath, fileSystem);
        BaseSnapshotCapturer capturer = new(repository, fileSystem);

        using RepositoryOperationLock operationLock = AcquireLock(repositoryPath);
        BaseCatalog catalog = capturer.Capture(
            operationLock,
            gameDirectory,
            relativePath,
            RepositoryFileKind.Payload);

        PayloadBaseEntry entry = Assert.Single(catalog.PayloadTargets);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(fileSystem, gameDirectory);
        string snapshotPath = repository.BaseSnapshots.ResolveFilePath(fingerprint, relativePath);

        Assert.Equal(PayloadBaseState.Present, entry.BaseState);
        Assert.Equal(fileSystem.ComputeFileIntegrity(sourcePath), entry.Integrity);
        Assert.Equal("payload", File.ReadAllText(snapshotPath));
        Assert.Equal(entry, capturer.TryGetPayloadEntry(gameDirectory, relativePath));
    }

    [Fact]
    public void Capture_WhenPayloadFileIsMissing_RegistersAbsentEntry()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string gameDirectory = directory.CreateDirectory("game");
        string relativePath = Path.Combine("Data", "config.json");
        FileSystemOperations fileSystem = CreateFileSystem();
        FileRepository repository = CreateRepository(repositoryPath, fileSystem);
        BaseSnapshotCapturer capturer = new(repository, fileSystem);

        using RepositoryOperationLock operationLock = AcquireLock(repositoryPath);
        BaseCatalog catalog = capturer.Capture(
            operationLock,
            gameDirectory,
            relativePath,
            RepositoryFileKind.Payload);

        PayloadBaseEntry entry = Assert.Single(catalog.PayloadTargets);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(fileSystem, gameDirectory);
        string snapshotPath = repository.BaseSnapshots.ResolveFilePath(fingerprint, relativePath);

        Assert.Equal(PayloadBaseState.Absent, entry.BaseState);
        Assert.Null(entry.Integrity);
        Assert.False(File.Exists(snapshotPath));
        Assert.Equal(entry, capturer.TryGetPayloadEntry(gameDirectory, relativePath));
    }

    [Fact]
    public void Capture_WhenEntryAlreadyExists_DoesNotOverwriteSnapshot()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string gameDirectory = directory.CreateDirectory("game");
        string relativePath = Path.Combine("Game_Data", "sharedassets0.assets");
        string sourcePath = directory.WriteFile(Path.Combine("game", relativePath), "original-assets");
        FileSystemOperations fileSystem = CreateFileSystem();
        FileRepository repository = CreateRepository(repositoryPath, fileSystem);
        BaseSnapshotCapturer capturer = new(repository, fileSystem);

        using RepositoryOperationLock operationLock = AcquireLock(repositoryPath);
        BaseCatalog firstCatalog = capturer.Capture(
            operationLock,
            gameDirectory,
            relativePath,
            RepositoryFileKind.Assets);
        File.WriteAllText(sourcePath, "changed-assets");
        BaseCatalog secondCatalog = capturer.Capture(
            operationLock,
            gameDirectory,
            relativePath,
            RepositoryFileKind.Assets);

        string fingerprint = GameInstanceIdentity.CreateFingerprint(fileSystem, gameDirectory);
        string snapshotPath = repository.BaseSnapshots.ResolveFilePath(fingerprint, relativePath);

        Assert.Equal(firstCatalog.CapturedAt, secondCatalog.CapturedAt);
        Assert.Equal(firstCatalog.AssetsFiles, secondCatalog.AssetsFiles);
        Assert.Equal("original-assets", File.ReadAllText(snapshotPath));
    }

    [Fact]
    public void Capture_WhenSourceChangesDuringCopy_ThrowsAndDoesNotWriteCatalog()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string gameDirectory = directory.CreateDirectory("game");
        string relativePath = Path.Combine("Game_Data", "sharedassets0.assets");
        string sourcePath = directory.WriteFile(Path.Combine("game", relativePath), "original-assets");
        MutatingCopyFileSystemOperations fileSystem = new(sourcePath);
        FileRepository repository = CreateRepository(repositoryPath, fileSystem);
        BaseSnapshotCapturer capturer = new(repository, fileSystem);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(fileSystem, gameDirectory);

        using RepositoryOperationLock operationLock = AcquireLock(repositoryPath);
        var exception = Assert.Throws<IOException>(() => capturer.Capture(
            operationLock,
            gameDirectory,
            relativePath,
            RepositoryFileKind.Assets));

        Assert.Contains("verification failed", exception.Message);
        Assert.Null(repository.BaseSnapshots.TryReadCatalog(fingerprint));
        Assert.Equal("changed-assets", File.ReadAllText(sourcePath));
        Assert.False(File.Exists(Path.Combine(
            repository.BaseSnapshots.GetBaseDirectory(fingerprint),
            BaseSnapshotStore.FilesDirectoryName,
            relativePath)));
    }

    [Fact]
    public void Capture_WhenOperationLockIsNull_ThrowsArgumentNullException()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string gameDirectory = directory.CreateDirectory("game");
        string relativePath = Path.Combine("Game_Data", "sharedassets0.assets");
        _ = directory.WriteFile(Path.Combine("game", relativePath), "original-assets");
        FileSystemOperations fileSystem = CreateFileSystem();
        FileRepository repository = CreateRepository(repositoryPath, fileSystem);
        BaseSnapshotCapturer capturer = new(repository, fileSystem);

        var exception = Assert.Throws<ArgumentNullException>(() => capturer.Capture(
            null!,
            gameDirectory,
            relativePath,
            RepositoryFileKind.Assets));

        Assert.Equal("operationLock", exception.ParamName);
    }

    [Fact]
    public void Capture_WhenOperationLockIsDisposed_ThrowsInvalidOperationException()
    {
        using RepositoryTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string gameDirectory = directory.CreateDirectory("game");
        string relativePath = Path.Combine("Game_Data", "sharedassets0.assets");
        _ = directory.WriteFile(Path.Combine("game", relativePath), "original-assets");
        FileSystemOperations fileSystem = CreateFileSystem();
        FileRepository repository = CreateRepository(repositoryPath, fileSystem);
        BaseSnapshotCapturer capturer = new(repository, fileSystem);
        RepositoryOperationLock operationLock = AcquireLock(repositoryPath);
        operationLock.Dispose();

        var exception = Assert.Throws<InvalidOperationException>(() => capturer.Capture(
            operationLock,
            gameDirectory,
            relativePath,
            RepositoryFileKind.Assets));

        Assert.Contains("no longer held", exception.Message);
    }

    private static FileRepository CreateRepository(string repositoryPath, IFileSystemOperations fileSystem)
    {
        return new FileRepository(repositoryPath, fileSystem, NullLoggerFactory.Instance);
    }

    private static FileSystemOperations CreateFileSystem()
    {
        return new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
    }

    private static RepositoryOperationLock AcquireLock(string repositoryPath)
    {
        return RepositoryOperationLock.Acquire(Path.Combine(repositoryPath, RepositoryService.LockFileName));
    }

    private sealed class MutatingCopyFileSystemOperations : IFileSystemOperations
    {
        private readonly FileSystemOperations _inner =
            new(NullLogger<FileSystemOperations>.Instance);

        private readonly string _sourcePath;

        public MutatingCopyFileSystemOperations(string sourcePath)
        {
            _sourcePath = sourcePath;
        }

        public Stream OpenRead(string path)
        {
            return _inner.OpenRead(path);
        }

        public FileIntegrity ComputeFileIntegrity(string path)
        {
            return _inner.ComputeFileIntegrity(path);
        }

        public FileAttributes GetAttributes(string path)
        {
            return _inner.GetAttributes(path);
        }

        public void WriteFileAtomically(string destinationPath, FileDestinationMode mode, Action<Stream> writer)
        {
            _inner.WriteFileAtomically(destinationPath, mode, writer);
        }

        public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode)
        {
            _inner.CopyFileAtomically(sourcePath, destinationPath, mode);

            if (TrustedPath.PathsEqual(sourcePath, _sourcePath))
            {
                File.WriteAllText(_sourcePath, "changed-assets");
            }
        }

        public void DeleteFile(string path)
        {
            _inner.DeleteFile(path);
        }

        public void EnsureDirectory(string path)
        {
            _inner.EnsureDirectory(path);
        }

        public void MoveDirectory(string sourcePath, string destinationPath)
        {
            _inner.MoveDirectory(sourcePath, destinationPath);
        }

        public void DeleteDirectoryTree(string path)
        {
            _inner.DeleteDirectoryTree(path);
        }
    }
}
