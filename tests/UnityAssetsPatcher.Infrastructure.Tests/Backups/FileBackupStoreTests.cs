using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Backups;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Backups;

public sealed class FileBackupStoreTests
{
    [Fact]
    public void StoreVerifiedCopy_WhenDestinationIsNew_CopiesAndVerifiesFile()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "prepared-install");
        string sourcePath = directory.WriteFile("game/data.assets", "original");
        FileBackupStore store = CreateStore(repositoryPath);

        FileIntegrity integrity = store.StoreVerifiedCopy(
            sourcePath,
            preparedDirectory,
            "backups/data.assets");

        string destinationPath = Path.Combine(preparedDirectory, "backups", "data.assets");
        Assert.Equal("original", File.ReadAllText(destinationPath));
        Assert.Equal(8, integrity.Length);
        Assert.Equal(
            "0682c5f2076f099c34cfdd15a9e063849ed437a49677e6fcc5b4198c76575be5",
            integrity.Sha256);
    }

    [Fact]
    public void StoreVerifiedCopy_WhenBackupPathTraversesOutsidePreparation_RejectsCopy()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "prepared-install");
        string sourcePath = directory.WriteFile("game/data.assets", "original");
        FileBackupStore store = CreateStore(repositoryPath);

        _ = Assert.Throws<IOException>(() => store.StoreVerifiedCopy(
            sourcePath,
            preparedDirectory,
            "../outside.assets"));

        Assert.False(File.Exists(Path.Combine(repositoryPath, ".temp", "outside.assets")));
    }

    [Fact]
    public void StoreVerifiedCopy_WhenPreparedDirectoryIsOutsideRepository_RejectsCopy()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        _ = directory.CreateDirectory("backup", ".temp");
        string outsideDirectory = directory.CreateDirectory("outside");
        string sourcePath = directory.WriteFile("game/data.assets", "original");
        FileBackupStore store = CreateStore(repositoryPath);

        _ = Assert.Throws<InvalidOperationException>(() => store.StoreVerifiedCopy(
            sourcePath,
            outsideDirectory,
            "data.assets"));

        Assert.False(File.Exists(Path.Combine(outsideDirectory, "data.assets")));
    }

    [Fact]
    public void StoreVerifiedCopy_WhenDestinationExists_PreservesExistingBackup()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "prepared-install");
        string sourcePath = directory.WriteFile("game/data.assets", "new");
        string destinationPath = directory.WriteFile(
            "backup/.temp/prepared-install/backups/data.assets",
            "existing");
        FileBackupStore store = CreateStore(repositoryPath);

        _ = Assert.Throws<IOException>(() => store.StoreVerifiedCopy(
            sourcePath,
            preparedDirectory,
            "backups/data.assets"));

        Assert.Equal("existing", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void StoreVerifiedCopy_WhenCopiedContentDoesNotMatch_RemovesInvalidBackup()
    {
        using BackupTestDirectory directory = new();
        string repositoryPath = directory.CreateDirectory("backup");
        string preparedDirectory = directory.CreateDirectory("backup", ".temp", "prepared-install");
        string sourcePath = directory.WriteFile("game/data.assets", "original");
        var fileSystem = new CorruptingCopyFileSystemOperations();
        var store = new FileBackupStore(
            repositoryPath,
            fileSystem,
            NullLogger<FileBackupStore>.Instance);
        string destinationPath = Path.Combine(preparedDirectory, "backups", "data.assets");

        IOException exception = Assert.Throws<IOException>(() => store.StoreVerifiedCopy(
            sourcePath,
            preparedDirectory,
            "backups/data.assets"));

        Assert.Contains("Backup verification failed", exception.Message);
        Assert.False(File.Exists(destinationPath));
    }

    private static FileBackupStore CreateStore(string repositoryPath)
    {
        var fileSystem = new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);

        return new FileBackupStore(
            repositoryPath,
            fileSystem,
            NullLogger<FileBackupStore>.Instance);
    }

    private sealed class CorruptingCopyFileSystemOperations : IFileSystemOperations
    {
        private readonly FileSystemOperations _inner =
            new(NullLogger<FileSystemOperations>.Instance);

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
            _inner.WriteFileAtomically(
                destinationPath,
                mode,
                stream =>
                {
                    using StreamWriter writer = new(stream, leaveOpen: true);

                    writer.Write("corrupt");
                });
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
