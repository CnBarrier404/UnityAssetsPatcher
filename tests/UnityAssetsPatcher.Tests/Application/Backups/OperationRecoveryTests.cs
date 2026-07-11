using UnityAssetsPatcher.Application.Backups;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Backups;

public sealed class OperationRecoveryTests
{
    [Fact]
    public void RecoverPendingInstall_RestoresAssetsAndRemovesPayloadAndTransaction()
    {
        using var scope = new TemporaryDirectories();
        string install = Path.Combine(scope.Backup, "pending-install");
        string asset = Path.Combine(scope.Game, "data.assets");
        string backup = Path.Combine(install, "assets", "data.assets");
        string payload = Path.Combine(scope.Game, "mod.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.WriteAllText(asset, "modified");
        File.WriteAllText(backup, "original");
        File.WriteAllText(payload, "payload");
        OperationJournalStore.Save(install, new OperationJournal(
            1, OperationKind.Install, OperationPhase.PayloadChanged, scope.Game,
            [new JournalPatchedFile(asset, backup)], [new JournalPayloadFile(payload)]));

        new ModBackupStore(scope.Backup).RecoverPendingTransactions();

        Assert.Equal("original", File.ReadAllText(asset));
        Assert.False(File.Exists(payload));
        Assert.False(Directory.Exists(install));
    }

    [Fact]
    public void RecoverPendingUninstall_RestoresModifiedAssetsAndStagedPayload()
    {
        using var scope = new TemporaryDirectories();
        string install = Path.Combine(scope.Backup, "pending-uninstall");
        string staging = Path.Combine(install, ".uninstall-staging");
        string asset = Path.Combine(scope.Game, "data.assets");
        string rollback = Path.Combine(scope.Game, ".data.assets.rollback");
        string payload = Path.Combine(scope.Game, "mod.bin");
        string stagedPayload = Path.Combine(staging, "payload.rollback");
        Directory.CreateDirectory(staging);
        File.WriteAllText(asset, "original");
        File.WriteAllText(rollback, "modified");
        File.WriteAllText(stagedPayload, "payload");
        OperationJournalStore.Save(install, new OperationJournal(
            1, OperationKind.Uninstall, OperationPhase.AssetsChanged, scope.Game,
            [new JournalPatchedFile(asset, "unused", rollback)],
            [new JournalPayloadFile(payload, stagedPayload)]));

        new ModBackupStore(scope.Backup).RecoverPendingTransactions();

        Assert.Equal("modified", File.ReadAllText(asset));
        Assert.Equal("payload", File.ReadAllText(payload));
        Assert.False(File.Exists(Path.Combine(install, OperationJournalStore.FileName)));
    }

    [Fact]
    public void Recover_CorruptRecordIsQuarantinedWithoutBreakingCatalog()
    {
        using var scope = new TemporaryDirectories();
        var store = new ModBackupStore(scope.Backup);
        string valid = Path.Combine(scope.Backup, "valid");
        string corrupt = Path.Combine(scope.Backup, "corrupt");
        store.Save(new InstallRecord(2, "game", 1, "id", DateTimeOffset.UtcNow,
            "mod", "1", "author", null, [], []), valid);
        Directory.CreateDirectory(corrupt);
        File.WriteAllText(Path.Combine(corrupt, "record.json"), "{ broken");

        store.RecoverPendingTransactions();

        Assert.Single(store.ListRecords());
        Assert.False(Directory.Exists(corrupt));
        Assert.Single(Directory.EnumerateDirectories(scope.Backup, "corrupt.quarantine-*"));
    }

    private sealed class TemporaryDirectories : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public string Backup { get; }
        public string Game { get; }

        public TemporaryDirectories()
        {
            Backup = Path.Combine(Root, "backup");
            Game = Path.Combine(Root, "game");
            Directory.CreateDirectory(Backup);
            Directory.CreateDirectory(Game);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
