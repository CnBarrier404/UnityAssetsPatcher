using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Backups;

public sealed class BackupRecoveryTests
{
    [Fact]
    public void RecoverInterruptedInstall_RestoresAssetsAndRemovesPayloadAndTempDirectory()
    {
        using var scope = new TemporaryDirectories();
        var store = new BackupRepository(scope.Backup);
        BackupRepositoryMetadata repository = store.LoadMetadata();
        string temporary = store.CreateTransactionDirectory();
        string rollbackDirectory = Path.Combine(temporary, "rollback");
        Directory.CreateDirectory(rollbackDirectory);
        string asset = Path.Combine(scope.Game, "data.assets");
        string rollback = Path.Combine(rollbackDirectory, "asset.bin");
        string payload = Path.Combine(scope.Game, "mod.bin");
        File.WriteAllText(asset, "modified");
        File.WriteAllText(rollback, "original");
        File.WriteAllText(payload, "payload");
        string installId = Guid.NewGuid().ToString("N");
        BackupTransactionStore.Save(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Install, installId, scope.Game,
            GameInstanceIdentity.CreateFingerprint(scope.Game),
            [
                new BackupTransactionFile(BackupFileKind.Assets, "data.assets",
                    TextIntegrity("original"), TextIntegrity("modified"), Path.Combine("rollback", "asset.bin")),
                new BackupTransactionFile(BackupFileKind.Payload, "mod.bin", null, TextIntegrity("payload")),
            ]));

        BackupRecoveryReport result = store.RecoverPendingTransactions();

        Assert.Equal(BackupRepositoryStatus.Recovered, result.Status);
        Assert.Equal("original", File.ReadAllText(asset));
        Assert.False(File.Exists(payload));
        Assert.False(Directory.Exists(temporary));
    }

    [Fact]
    public void RecoverInterruptedUninstall_RestoresInstalledAssetsAndPayload()
    {
        using var scope = new TemporaryDirectories();
        var store = new BackupRepository(scope.Backup);
        BackupRepositoryMetadata repository = store.LoadMetadata();
        string installId = Guid.NewGuid().ToString("N");
        store.WriteRecord(new InstallRecord(repository.RepositoryId,
            GameInstanceIdentity.CreateFingerprint(scope.Game), 1, installId, DateTimeOffset.UnixEpoch,
            "Mod", "1", "Tests", null, [], []), store.GetInstallDirectory(installId));
        string temporary = store.CreateTransactionDirectory();
        string rollbackDirectory = Path.Combine(temporary, "rollback");
        Directory.CreateDirectory(rollbackDirectory);
        string asset = Path.Combine(scope.Game, "data.assets");
        string assetRollback = Path.Combine(rollbackDirectory, "asset.bin");
        string payload = Path.Combine(scope.Game, "mod.bin");
        string payloadRollback = Path.Combine(rollbackDirectory, "payload.bin");
        File.WriteAllText(asset, "original");
        File.WriteAllText(assetRollback, "modified");
        File.WriteAllText(payloadRollback, "payload");
        BackupTransactionStore.Save(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Uninstall, installId, scope.Game,
            GameInstanceIdentity.CreateFingerprint(scope.Game),
            [
                new BackupTransactionFile(BackupFileKind.Assets, "data.assets",
                    TextIntegrity("modified"), TextIntegrity("original"), Path.Combine("rollback", "asset.bin")),
                new BackupTransactionFile(BackupFileKind.Payload, "mod.bin",
                    TextIntegrity("payload"), null, Path.Combine("rollback", "payload.bin")),
            ]));

        BackupRecoveryReport result = store.RecoverPendingTransactions();

        Assert.Equal(BackupRepositoryStatus.Recovered, result.Status);
        Assert.Equal("modified", File.ReadAllText(asset));
        Assert.Equal("payload", File.ReadAllText(payload));
        Assert.False(Directory.Exists(temporary));
    }

    [Fact]
    public void Recover_UnknownTargetState_LeavesEvidenceAndLocksRepository()
    {
        using var scope = new TemporaryDirectories();
        var store = new BackupRepository(scope.Backup);
        BackupRepositoryMetadata repository = store.LoadMetadata();
        string temporary = store.CreateTransactionDirectory();
        string asset = Path.Combine(scope.Game, "data.assets");
        File.WriteAllText(asset, "unknown");
        BackupTransactionStore.Save(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Install, Guid.NewGuid().ToString("N"), scope.Game,
            GameInstanceIdentity.CreateFingerprint(scope.Game),
            [
                new BackupTransactionFile(BackupFileKind.Assets, "data.assets",
                    TextIntegrity("original"), TextIntegrity("modified"), Path.Combine("rollback", "asset.bin"))
            ]));

        BackupRecoveryReport result = store.RecoverPendingTransactions();

        Assert.Equal(BackupRepositoryStatus.Locked, result.Status);
        Assert.Equal("unknown", File.ReadAllText(asset));
        Assert.True(Directory.Exists(temporary));
    }

    private static FileIntegrity TextIntegrity(string value) =>
        FileIntegrity.Create(System.Text.Encoding.UTF8.GetBytes(value));

    private sealed class TemporaryDirectories : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public string Backup { get; }
        public string Game { get; }

        public TemporaryDirectories()
        {
            Backup = Path.Combine(Root, "backup");
            Game = Path.Combine(Root, "game");
            Directory.CreateDirectory(Game);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
