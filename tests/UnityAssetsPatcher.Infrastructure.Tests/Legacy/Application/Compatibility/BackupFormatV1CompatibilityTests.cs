using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Compatibility;

public sealed class BackupFormatV1CompatibilityTests
{
    [Fact]
    public void FormatVersion1Repository_WhenLoaded_PreservesMetadataAndInstallRecord()
    {
        using CompatibilityTestDirectory scope = new();
        CompatibilityFixture.InitializeRepository(scope.Backup);
        CompatibilityFixture.CopyInstallRecord(
            scope.Backup,
            "committed-install-v1",
            new string('0', 64));
        var repository = TestDependencies.CreateBackupRepository(
            scope.Backup,
            TestDependencies.FileSystemOperations);

        BackupRepositoryMetadata metadata = repository.LoadMetadata();
        InstallRecordEntry entry = Assert.Single(repository.ListRecords());
        InstallRecord record = entry.Record;

        Assert.Equal(1, metadata.FormatVersion);
        Assert.Equal(CompatibilityFixture.RepositoryId, metadata.RepositoryId);
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
    public void UncommittedInstallTransaction_WhenPreviewedAndRecovered_RollsBackFixtureState()
    {
        using CompatibilityTestDirectory scope = new();
        BackupRepository repository = InitializeRepository(scope);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, scope.Game);
        string transactionDirectory = CompatibilityFixture.CopyTransaction(
            scope.Backup,
            "install-transaction-uncommitted-v1.json",
            fingerprint);
        WriteGameFile(scope, "data.assets", "modified");
        WriteGameFile(scope, "mod.bin", "payload");
        WriteTransactionFile(transactionDirectory, "rollback", "data.assets", "original");

        BackupRecoveryPreview preview = repository.PreviewPendingTransaction(scope.Game);

        Assert.Equal(BackupRepositoryStatus.RecoveryRequired, preview.Status);
        Assert.Equal("install", preview.Kind);
        Assert.Equal("pending-install-v1", preview.InstallId);
        Assert.Equal(BackupRecoveryPlanAction.RollBack, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.Collection(
            preview.Files,
            file =>
            {
                Assert.Equal("Game_Data/data.assets", file.RelativePath);
                Assert.Equal(BackupRecoveryFileAction.Restore, file.Action);
            },
            file =>
            {
                Assert.Equal("Game_Data/mod.bin", file.RelativePath);
                Assert.Equal(BackupRecoveryFileAction.Delete, file.Action);
            });

        BackupRecoveryReport report = repository.RecoverPendingTransactions(scope.Game);

        Assert.Equal(BackupRepositoryStatus.Recovered, report.Status);
        BackupRecoveryOperation operation = Assert.Single(report.Operations);
        Assert.Equal("install", operation.Kind);
        Assert.Equal("pending-install-v1", operation.InstallId);
        Assert.Equal("rolled back", operation.Action);
        Assert.Equal("original", ReadGameFile(scope, "data.assets"));
        Assert.False(File.Exists(Path.Combine(scope.GameData, "mod.bin")));
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void CommittedInstallTransaction_WhenPreviewedAndRecovered_CompletesFixtureCleanup()
    {
        using CompatibilityTestDirectory scope = new();
        BackupRepository repository = InitializeRepository(scope);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, scope.Game);
        CompatibilityFixture.CopyInstallRecord(scope.Backup, "committed-install-v1", fingerprint);
        string transactionDirectory = CompatibilityFixture.CopyTransaction(
            scope.Backup,
            "install-transaction-committed-v1.json",
            fingerprint);
        WriteGameFile(scope, "data.assets", "modified");
        WriteGameFile(scope, "mod.bin", "payload");

        BackupRecoveryPreview preview = repository.PreviewPendingTransaction(scope.Game);

        Assert.Equal(BackupRepositoryStatus.RecoveryRequired, preview.Status);
        Assert.Equal(BackupRecoveryPlanAction.CompleteCleanup, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.All(preview.Files, file => Assert.Equal(BackupRecoveryFileAction.NoChange, file.Action));

        BackupRecoveryReport report = repository.RecoverPendingTransactions(scope.Game);

        Assert.Equal(BackupRepositoryStatus.Recovered, report.Status);
        Assert.Equal("completed cleanup", Assert.Single(report.Operations).Action);
        Assert.Equal("modified", ReadGameFile(scope, "data.assets"));
        Assert.Equal("payload", ReadGameFile(scope, "mod.bin"));
        Assert.Single(repository.ListRecords());
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void UncommittedUninstallTransaction_WhenPreviewedAndRecovered_RestoresFixtureState()
    {
        using CompatibilityTestDirectory scope = new();
        BackupRepository repository = InitializeRepository(scope);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, scope.Game);
        CompatibilityFixture.CopyInstallRecord(scope.Backup, "pending-uninstall-v1", fingerprint);
        string transactionDirectory = CompatibilityFixture.CopyTransaction(
            scope.Backup,
            "uninstall-transaction-uncommitted-v1.json",
            fingerprint);
        WriteGameFile(scope, "data.assets", "original");
        WriteTransactionFile(transactionDirectory, "rollback", "data.assets", "installed-assets");
        WriteTransactionFile(transactionDirectory, "rollback", "mod.bin", "payload");

        BackupRecoveryPreview preview = repository.PreviewPendingTransaction(scope.Game);

        Assert.Equal(BackupRepositoryStatus.RecoveryRequired, preview.Status);
        Assert.Equal("uninstall", preview.Kind);
        Assert.Equal("pending-uninstall-v1", preview.InstallId);
        Assert.Equal(BackupRecoveryPlanAction.RollBack, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.All(preview.Files, file => Assert.Equal(BackupRecoveryFileAction.Restore, file.Action));

        BackupRecoveryReport report = repository.RecoverPendingTransactions(scope.Game);

        Assert.Equal(BackupRepositoryStatus.Recovered, report.Status);
        Assert.Equal("rolled back", Assert.Single(report.Operations).Action);
        Assert.Equal("installed-assets", ReadGameFile(scope, "data.assets"));
        Assert.Equal("payload", ReadGameFile(scope, "mod.bin"));
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void CommittedUninstallTransaction_WhenPreviewedAndRecovered_CompletesFixtureCleanup()
    {
        using CompatibilityTestDirectory scope = new();
        BackupRepository repository = InitializeRepository(scope);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, scope.Game);
        string transactionDirectory = CompatibilityFixture.CopyTransaction(
            scope.Backup,
            "uninstall-transaction-committed-v1.json",
            fingerprint);
        Directory.CreateDirectory(Path.Combine(transactionDirectory, "removed-install"));
        WriteGameFile(scope, "data.assets", "original");

        BackupRecoveryPreview preview = repository.PreviewPendingTransaction(scope.Game);

        Assert.Equal(BackupRepositoryStatus.RecoveryRequired, preview.Status);
        Assert.Equal("uninstall", preview.Kind);
        Assert.Equal("committed-uninstall-v1", preview.InstallId);
        Assert.Equal(BackupRecoveryPlanAction.CompleteCleanup, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.All(preview.Files, file => Assert.Equal(BackupRecoveryFileAction.NoChange, file.Action));

        BackupRecoveryReport report = repository.RecoverPendingTransactions(scope.Game);

        Assert.Equal(BackupRepositoryStatus.Recovered, report.Status);
        Assert.Equal("completed cleanup", Assert.Single(report.Operations).Action);
        Assert.Equal("original", ReadGameFile(scope, "data.assets"));
        Assert.False(File.Exists(Path.Combine(scope.GameData, "mod.bin")));
        Assert.False(Directory.Exists(transactionDirectory));
    }

    private static BackupRepository InitializeRepository(CompatibilityTestDirectory scope)
    {
        CompatibilityFixture.InitializeRepository(scope.Backup);

        return TestDependencies.CreateBackupRepository(
            scope.Backup,
            TestDependencies.FileSystemOperations);
    }

    private static void WriteGameFile(CompatibilityTestDirectory scope, string fileName, string contents)
    {
        File.WriteAllText(Path.Combine(scope.GameData, fileName), contents);
    }

    private static string ReadGameFile(CompatibilityTestDirectory scope, string fileName)
    {
        return File.ReadAllText(Path.Combine(scope.GameData, fileName));
    }

    private static void WriteTransactionFile(
        string transactionDirectory,
        string directoryName,
        string fileName,
        string contents)
    {
        string directory = Path.Combine(transactionDirectory, directoryName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), contents);
    }
}
