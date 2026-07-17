using UnityAssetsPatcher.Application.Backups;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Backups;

public sealed class BackupRepositoryTests
{
    [Fact]
    public void LoadMetadata_CreatesMinimalRepositoryLayout()
    {
        string root = CreateRoot();
        try
        {
            var store = new BackupRepository(Path.Combine(root, "backup"));

            BackupRepositoryMetadata repository = store.LoadMetadata();

            Assert.Equal(BackupRepository.CurrentRepositoryFormatVersion, repository.FormatVersion);
            Assert.Matches("^[0-9a-f]{32}$", repository.RepositoryId);
            Assert.True(File.Exists(Path.Combine(store.BackupDirectory, BackupRepository.RepositoryFileName)));
            Assert.True(Directory.Exists(store.InstalledDirectory));
            Assert.False(Directory.Exists(store.TransactionDirectory));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CreateTransactionDirectory_RejectsExistingTransaction()
    {
        string root = CreateRoot();
        try
        {
            var store = new BackupRepository(Path.Combine(root, "backup"));
            _ = store.LoadMetadata();
            Assert.Equal(store.TransactionDirectory, store.CreateTransactionDirectory());
            Assert.Throws<InvalidOperationException>(() => store.CreateTransactionDirectory());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void RestoreAtomically_ReplacesTargetThroughTemporarySibling()
    {
        string root = CreateRoot();
        try
        {
            string backup = Path.Combine(root, "backup.bin");
            string target = Path.Combine(root, "target.bin");
            File.WriteAllText(backup, "original");
            File.WriteAllText(target, "modified");

            BackupFileSystem.RestoreAtomically(backup, target);

            Assert.Equal("original", File.ReadAllText(target));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SaveAndListRecords_UsesInstalledDirectoryAndRepositoryIdentity()
    {
        string root = CreateRoot();
        try
        {
            var store = new BackupRepository(Path.Combine(root, "backup"));
            string repositoryId = store.LoadMetadata().RepositoryId;
            string installId = Guid.NewGuid().ToString("N");
            string installDirectory = store.GetInstallDirectory(installId);
            var record = new InstallRecord(repositoryId, "game", 1, installId, DateTimeOffset.UnixEpoch,
                "Mod", "1.0", "Author", "Game", [], []);

            store.WriteRecord(record, installDirectory);

            InstallRecordEntry entry = Assert.Single(store.ListRecords());
            Assert.Equal(installDirectory, entry.InstallDirectory);
            Assert.Equal(installId, entry.Record.Id);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Save_WhenRecordContainsTraversal_RejectsRecordBeforeWriting()
    {
        string root = CreateRoot();
        try
        {
            var store = new BackupRepository(Path.Combine(root, "backup"));
            string repositoryId = store.LoadMetadata().RepositoryId;
            string installId = Guid.NewGuid().ToString("N");
            string installDirectory = store.GetInstallDirectory(installId);
            FileIntegrity integrity = FileIntegrity.Create([1]);
            var record = new InstallRecord(repositoryId, "game", 1, installId, DateTimeOffset.UnixEpoch,
                "Mod", "1.0", "Author", "Game",
                [
                    new InstallRecordPatchedFile("target", "../outside.assets", "assets/original.assets", 1, 1,
                        integrity, integrity)
                ], []);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                store.WriteRecord(record, installDirectory));

            Assert.Contains("path is not trusted", exception.Message);
            Assert.False(File.Exists(Path.Combine(installDirectory, "record.json")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
