using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Tests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Backups;

public sealed class BackupRecoveryTests
{
    [Fact]
    public void CheckInterruptedInstall_ReportsRecoveryRequiredWithoutChangingFiles()
    {
        using var scope = new TemporaryDirectories();
        var store = CreateBackupRepository(scope.Backup);
        BackupRepositoryMetadata repository = store.LoadMetadata();
        string temporary = store.CreateTransactionDirectory();
        string asset = Path.Combine(scope.Game, "data.assets");
        File.WriteAllText(asset, "modified");
        SaveTransaction(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Install, Guid.NewGuid().ToString("N"),
            GameInstanceIdentity.CreateFingerprint(scope.Game),
            [
                new BackupTransactionFile(BackupFileKind.Assets, "data.assets",
                    TextIntegrity("original"), TextIntegrity("modified"), Path.Combine("rollback", "asset.bin"))
            ]));

        BackupRecoveryReport result = store.CheckPendingTransactions();

        Assert.Equal(BackupRepositoryStatus.RecoveryRequired, result.Status);
        Assert.Equal("modified", File.ReadAllText(asset));
        Assert.True(Directory.Exists(temporary));
    }

    [Fact]
    public void RecoverInterruptedInstall_RestoresAssetsAndRemovesPayloadAndTempDirectory()
    {
        using var scope = new TemporaryDirectories();
        var store = CreateBackupRepository(scope.Backup);
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
        SaveTransaction(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Install, installId,
            GameInstanceIdentity.CreateFingerprint(scope.Game),
            [
                new BackupTransactionFile(BackupFileKind.Assets, "data.assets",
                    TextIntegrity("original"), TextIntegrity("modified"), Path.Combine("rollback", "asset.bin")),
                new BackupTransactionFile(BackupFileKind.Payload, "mod.bin", null, TextIntegrity("payload")),
            ]));

        BackupRecoveryReport result = store.RecoverPendingTransactions(scope.Game);

        Assert.Equal(BackupRepositoryStatus.Recovered, result.Status);
        Assert.Equal("original", File.ReadAllText(asset));
        Assert.False(File.Exists(payload));
        Assert.False(Directory.Exists(temporary));
    }

    [Fact]
    public void RecoverInterruptedUninstall_RestoresInstalledAssetsAndPayload()
    {
        using var scope = new TemporaryDirectories();
        var store = CreateBackupRepository(scope.Backup);
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
        SaveTransaction(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Uninstall, installId,
            GameInstanceIdentity.CreateFingerprint(scope.Game),
            [
                new BackupTransactionFile(BackupFileKind.Assets, "data.assets",
                    TextIntegrity("modified"), TextIntegrity("original"), Path.Combine("rollback", "asset.bin")),
                new BackupTransactionFile(BackupFileKind.Payload, "mod.bin",
                    TextIntegrity("payload"), null, Path.Combine("rollback", "payload.bin")),
            ]));

        BackupRecoveryReport result = store.RecoverPendingTransactions(scope.Game);

        Assert.Equal(BackupRepositoryStatus.Recovered, result.Status);
        Assert.Equal("modified", File.ReadAllText(asset));
        Assert.Equal("payload", File.ReadAllText(payload));
        Assert.False(Directory.Exists(temporary));
    }

    [Fact]
    public void Recover_UnknownTargetState_LeavesEvidenceAndLocksRepository()
    {
        using var scope = new TemporaryDirectories();
        var store = CreateBackupRepository(scope.Backup);
        BackupRepositoryMetadata repository = store.LoadMetadata();
        string temporary = store.CreateTransactionDirectory();
        string asset = Path.Combine(scope.Game, "data.assets");
        File.WriteAllText(asset, "unknown");
        SaveTransaction(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Install, Guid.NewGuid().ToString("N"),
            GameInstanceIdentity.CreateFingerprint(scope.Game),
            [
                new BackupTransactionFile(BackupFileKind.Assets, "data.assets",
                    TextIntegrity("original"), TextIntegrity("modified"), Path.Combine("rollback", "asset.bin"))
            ]));

        BackupRecoveryReport result = store.RecoverPendingTransactions(scope.Game);

        Assert.Equal(BackupRepositoryStatus.Locked, result.Status);
        Assert.Equal("unknown", File.ReadAllText(asset));
        Assert.True(Directory.Exists(temporary));
    }

    [Fact]
    public void PreviewInterruptedInstall_ListsActionsWithoutChangingFiles()
    {
        using var scope = new TemporaryDirectories();
        var store = CreateBackupRepository(scope.Backup);
        BackupRepositoryMetadata repository = store.LoadMetadata();
        string temporary = store.CreateTransactionDirectory();
        string rollbackDirectory = Path.Combine(temporary, "rollback");
        Directory.CreateDirectory(rollbackDirectory);
        string asset = Path.Combine(scope.Game, "data.assets");
        string payload = Path.Combine(scope.Game, "mod.bin");
        File.WriteAllText(asset, "modified");
        File.WriteAllText(payload, "payload");
        File.WriteAllText(Path.Combine(rollbackDirectory, "asset.bin"), "original");
        SaveTransaction(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Install, Guid.NewGuid().ToString("N"),
            GameInstanceIdentity.CreateFingerprint(scope.Game),
            [
                new BackupTransactionFile(BackupFileKind.Assets, "data.assets",
                    TextIntegrity("original"), TextIntegrity("modified"), Path.Combine("rollback", "asset.bin")),
                new BackupTransactionFile(BackupFileKind.Payload, "mod.bin", null, TextIntegrity("payload")),
            ]));

        BackupRecoveryPreview preview = store.PreviewPendingTransaction(scope.Game);

        Assert.True(preview.CanRecover);
        Assert.Equal(BackupRecoveryPlanAction.RollBack, preview.Action);
        Assert.Collection(preview.Files,
            file => Assert.Equal(BackupRecoveryFileAction.Restore, file.Action),
            file => Assert.Equal(BackupRecoveryFileAction.Delete, file.Action));
        Assert.Equal("modified", File.ReadAllText(asset));
        Assert.Equal("payload", File.ReadAllText(payload));
        Assert.True(Directory.Exists(temporary));
    }

    [Fact]
    public void Recover_LegacyJournalClaimsExternalRoot_DoesNotDeleteExternalFile()
    {
        using var scope = new TemporaryDirectories();
        var store = CreateBackupRepository(scope.Backup);
        BackupRepositoryMetadata repository = store.LoadMetadata();
        string temporary = store.CreateTransactionDirectory();
        string externalDirectory = Path.Combine(scope.Root, "external");
        Directory.CreateDirectory(externalDirectory);
        string externalFile = Path.Combine(externalDirectory, "important.txt");
        File.WriteAllText(externalFile, "important");
        SaveTransaction(temporary, new BackupTransaction(
            repository.RepositoryId, BackupOperationKind.Install, Guid.NewGuid().ToString("N"),
            GameInstanceIdentity.CreateFingerprint(externalDirectory),
            [new BackupTransactionFile(BackupFileKind.Payload, "important.txt", null, TextIntegrity("important"))]));
        string journalPath = Path.Combine(temporary, BackupTransactionStore.FileName);
        string journal = File.ReadAllText(journalPath).Replace(
            "\"gameInstanceFingerprint\"",
            $"\"gameDirectory\": {System.Text.Json.JsonSerializer.Serialize(externalDirectory)},\n  \"gameInstanceFingerprint\"",
            StringComparison.Ordinal);
        File.WriteAllText(journalPath, journal);

        BackupRecoveryPreview preview = store.PreviewPendingTransaction(scope.Game);
        BackupRecoveryReport result = store.RecoverPendingTransactions(scope.Game);

        Assert.False(preview.CanRecover);
        Assert.Equal(BackupRepositoryStatus.Locked, result.Status);
        Assert.Equal("important", File.ReadAllText(externalFile));
        Assert.True(Directory.Exists(temporary));
    }

    private static BackupRepository CreateBackupRepository(string backupDirectory)
    {
        return new BackupRepository(
            backupDirectory,
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations);
    }

    private static void SaveTransaction(string transactionDirectory, BackupTransaction transaction)
    {
        BackupTransactionStore.Save(
            TestDependencies.FileOperations,
            TestDependencies.DirectoryOperations,
            transactionDirectory,
            transaction);
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
