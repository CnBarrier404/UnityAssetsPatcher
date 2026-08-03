using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Uninstallation;

public sealed class UninstallPathValidatorTests
{
    [Fact]
    public void ResolveRecordPaths_WhenPatchedTargetContainsDirectory_RejectsRecord()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string backupDirectory = Path.Combine(root, "backup");
        string installDirectory = Path.Combine(backupDirectory, BackupRepository.InstalledDirectoryName, "install-1");
        string gameDirectory = Path.Combine(root, "game");
        Directory.CreateDirectory(installDirectory);
        Directory.CreateDirectory(gameDirectory);
        var integrity = new FileIntegrity(0, new string('0', 64));
        var record = new InstallRecord(
            "repository",
            new string('0', 64),
            1,
            "install-1",
            DateTimeOffset.UnixEpoch,
            "Test Mod",
            "1.0.0",
            "tests",
            null,
            [
                new InstallRecordPatchedFile(
                    Path.Combine("Data", "sharedassets0.assets"),
                    Path.Combine("Game_Data", "sharedassets0.assets"),
                    Path.Combine("backups", "sharedassets0.assets"),
                    0,
                    0,
                    integrity,
                    integrity),
            ],
            []);

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                UninstallPathValidator.ResolveRecordPaths(
                    TestDependencies.FileSystemOperations,
                    backupDirectory,
                    installDirectory,
                    gameDirectory,
                    record));

            Assert.Equal(
                $"Patched target must be a file name: {Path.Combine("Data", "sharedassets0.assets")}",
                exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ResolveRecordPaths_WhenCopiedDestinationTraversesReparsePoint_WrapsPathError()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string backupDirectory = Path.Combine(root, "backup");
        string installDirectory = Path.Combine(backupDirectory, BackupRepository.InstalledDirectoryName, "install-1");
        string gameDirectory = Path.Combine(root, "game");
        string reparseDirectory = Path.Combine(gameDirectory, "Game_Data");
        var integrity = new FileIntegrity(0, new string('0', 64));
        var record = new InstallRecord(
            "repository",
            new string('0', 64),
            1,
            "install-1",
            DateTimeOffset.UnixEpoch,
            "Test Mod",
            "1.0.0",
            "tests",
            null,
            [],
            [
                new InstallRecordCopiedFile(
                    "resources/modassets.resource",
                    Path.Combine("Game_Data", "modassets.resource"),
                    integrity),
            ]);
        IFileSystemOperations fileSystemOperations = new ReparsePointFileSystemOperations(
            Path.Combine(backupDirectory, BackupRepository.InstalledDirectoryName),
            installDirectory,
            gameDirectory,
            reparseDirectory);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            UninstallPathValidator.ResolveRecordPaths(
                fileSystemOperations,
                backupDirectory,
                installDirectory,
                gameDirectory,
                record));

        Assert.Equal(
            "Uninstall payload destination path must be inside its trusted directory: " +
            Path.Combine("Game_Data", "modassets.resource"),
            exception.Message);
        Assert.IsType<IOException>(exception.InnerException);
    }

    private sealed class ReparsePointFileSystemOperations : IFileSystemOperations
    {
        private readonly string _backupInstalledDirectory;
        private readonly string _installDirectory;
        private readonly string _gameDirectory;
        private readonly string _reparseDirectory;

        public ReparsePointFileSystemOperations(
            string backupInstalledDirectory,
            string installDirectory,
            string gameDirectory,
            string reparseDirectory)
        {
            _backupInstalledDirectory = TrustedPath.NormalizeAbsolutePath(backupInstalledDirectory);
            _installDirectory = TrustedPath.NormalizeAbsolutePath(installDirectory);
            _gameDirectory = TrustedPath.NormalizeAbsolutePath(gameDirectory);
            _reparseDirectory = TrustedPath.NormalizeAbsolutePath(reparseDirectory);
        }

        public Stream OpenRead(string path)
        {
            throw new NotSupportedException();
        }

        public FileIntegrity ComputeFileIntegrity(string path)
        {
            throw new NotSupportedException();
        }

        public FileAttributes GetAttributes(string path)
        {
            string fullPath = TrustedPath.NormalizeAbsolutePath(path);

            if (TrustedPath.PathsEqual(fullPath, _reparseDirectory))
            {
                return FileAttributes.Directory | FileAttributes.ReparsePoint;
            }

            if (TrustedPath.PathsEqual(fullPath, _backupInstalledDirectory) ||
                TrustedPath.PathsEqual(fullPath, _installDirectory) ||
                TrustedPath.PathsEqual(fullPath, _gameDirectory))
            {
                return FileAttributes.Directory;
            }

            throw new FileNotFoundException(null, fullPath);
        }

        public void WriteFileAtomically(string destinationPath, FileDestinationMode mode, Action<Stream> writer)
        {
            throw new NotSupportedException();
        }

        public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode)
        {
            throw new NotSupportedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotSupportedException();
        }

        public void EnsureDirectory(string path)
        {
            throw new NotSupportedException();
        }

        public void MoveDirectory(string sourcePath, string destinationPath)
        {
            throw new NotSupportedException();
        }

        public void DeleteDirectoryTree(string path)
        {
            throw new NotSupportedException();
        }
    }
}
