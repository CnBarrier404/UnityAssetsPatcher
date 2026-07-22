using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Tests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class UninstallIntegrityTests
{
    [Theory]
    [InlineData(TrackedFile.Assets)]
    [InlineData(TrackedFile.Backup)]
    [InlineData(TrackedFile.Payload)]
    public void Preview_WhenTrackedFileIsReplacedWithSameLength_BlocksUninstall(TrackedFile trackedFile)
    {
        using Scenario scenario = Scenario.Create();
        scenario.ReplaceWithSameLength(trackedFile);

        UninstallPreviewResult preview = scenario.Workflow.Preview(
            new UninstallPreviewRequest(scenario.InstallId, scenario.GameDirectory));

        Assert.False(preview.CanUninstall);
        UninstallPreviewRestoredFileResult restored = Assert.Single(preview.RestoredFiles);
        UninstallPreviewDeletedFileResult deleted = Assert.Single(preview.DeletedFiles);
        Assert.Equal(
            trackedFile == TrackedFile.Assets ? FileIntegrityStatus.Modified : FileIntegrityStatus.Matches,
            restored.TargetStatus);
        Assert.Equal(
            trackedFile == TrackedFile.Backup ? FileIntegrityStatus.Modified : FileIntegrityStatus.Matches,
            restored.BackupStatus);
        Assert.Equal(
            trackedFile == TrackedFile.Payload ? FileIntegrityStatus.Modified : FileIntegrityStatus.Matches,
            deleted.Status);
    }

    [Theory]
    [InlineData(TrackedFile.Assets)]
    [InlineData(TrackedFile.Backup)]
    [InlineData(TrackedFile.Payload)]
    public void Uninstall_WhenTrackedFileChangesAfterPreview_BlocksBeforeAnyMutation(TrackedFile trackedFile)
    {
        using Scenario scenario = Scenario.Create();
        UninstallPreviewResult preview = scenario.Workflow.Preview(
            new UninstallPreviewRequest(scenario.InstallId, scenario.GameDirectory));
        Assert.True(preview.CanUninstall);

        scenario.ReplaceWithSameLength(trackedFile);

        Assert.Throws<InvalidOperationException>(() => scenario.Workflow.Uninstall(
            new UninstallModRequest(scenario.InstallId, scenario.GameDirectory)));
        Assert.Equal(trackedFile == TrackedFile.Assets ? "changed" : "patched",
            File.ReadAllText(scenario.AssetsPath));
        Assert.Equal(trackedFile == TrackedFile.Backup ? "replaced" : "original",
            File.ReadAllText(scenario.BackupPath));
        Assert.Equal(trackedFile == TrackedFile.Payload ? "altered" : "payload",
            File.ReadAllText(scenario.PayloadPath));
        Assert.True(File.Exists(Path.Combine(scenario.InstallDirectory, "record.json")));
    }

    [Fact]
    public void Uninstall_WhenPayloadIsAlreadyMissing_AllowsRestoreAndRecordCleanup()
    {
        using Scenario scenario = Scenario.Create();
        File.Delete(scenario.PayloadPath);

        UninstallPreviewResult preview = scenario.Workflow.Preview(
            new UninstallPreviewRequest(scenario.InstallId, scenario.GameDirectory));
        UninstallPreviewDeletedFileResult payload = Assert.Single(preview.DeletedFiles);

        Assert.True(preview.CanUninstall);
        Assert.Equal(FileIntegrityStatus.Missing, payload.Status);

        UninstallModResult result = scenario.Workflow.Uninstall(
            new UninstallModRequest(scenario.InstallId, scenario.GameDirectory));

        Assert.Equal("original", File.ReadAllText(scenario.AssetsPath));
        Assert.False(Assert.Single(result.DeletedFiles).Deleted);
        Assert.False(Directory.Exists(scenario.InstallDirectory));
    }

    public enum TrackedFile
    {
        Assets,
        Backup,
        Payload,
    }

    private sealed class Scenario : IDisposable
    {
        public required string Root { get; init; }
        public required string GameDirectory { get; init; }
        public required string InstallDirectory { get; init; }
        public required string InstallId { get; init; }
        public required string AssetsPath { get; init; }
        public required string BackupPath { get; init; }
        public required string PayloadPath { get; init; }
        public required UninstallModWorkflow Workflow { get; init; }

        public static Scenario Create()
        {
            string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string gameDirectory = Path.Combine(root, "game");
            string gameDataDirectory = Path.Combine(gameDirectory, "Game_Data");
            string backupDirectory = Path.Combine(root, "backup");
            string installDirectory = Path.Combine(backupDirectory, "install");
            string assetsPath = Path.Combine(gameDataDirectory, "sharedassets0.assets");
            string backupPath = Path.Combine(installDirectory, "assets", "sharedassets0.assets");
            string payloadPath = Path.Combine(gameDataDirectory, "mod.resource");
            Directory.CreateDirectory(gameDataDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.WriteAllText(assetsPath, "patched");
            File.WriteAllText(backupPath, "original");
            File.WriteAllText(payloadPath, "payload");

            var record = new InstallRecord(
                string.Empty,
                GameInstanceIdentity.CreateFingerprint(gameDirectory),
                1,
                "install-1",
                DateTimeOffset.UnixEpoch,
                "Test Mod",
                "1.0.0",
                "tests",
                null,
                [
                    new InstallRecordPatchedFile(
                        Path.GetFileName(assetsPath),
                        Path.GetRelativePath(gameDirectory, assetsPath),
                        Path.GetRelativePath(installDirectory, backupPath),
                        1,
                        1,
                        FileIntegrity.Create(assetsPath),
                        FileIntegrity.Create(backupPath)),
                ],
                [
                    new InstallRecordCopiedFile(
                        Path.GetFileName(payloadPath),
                        Path.GetRelativePath(gameDirectory, payloadPath),
                        FileIntegrity.Create(payloadPath)),
                ]);
            var store = new BackupRepository(
                backupDirectory,
                TestDependencies.FileOperations,
                TestDependencies.DirectoryOperations);
            string committedInstallDirectory = store.GetInstallDirectory(record.Id);
            _ = store.LoadMetadata();
            Directory.Move(installDirectory, committedInstallDirectory);
            store.WriteRecord(record, committedInstallDirectory);
            string committedBackupPath = Path.Combine(committedInstallDirectory, "assets", "sharedassets0.assets");

            return new Scenario
            {
                Root = root,
                GameDirectory = gameDirectory,
                InstallDirectory = committedInstallDirectory,
                InstallId = record.Id,
                AssetsPath = assetsPath,
                BackupPath = committedBackupPath,
                PayloadPath = payloadPath,
                Workflow = new UninstallModWorkflow(
                    new UninstallPlanner(store, new GameDirectoryResolver([])),
                    new UninstallExecutor(
                        store,
                        TestDependencies.FileOperations,
                        TestDependencies.DirectoryOperations),
                    store),
            };
        }

        public void ReplaceWithSameLength(TrackedFile trackedFile)
        {
            (string path, string contents) = trackedFile switch
            {
                TrackedFile.Assets => (AssetsPath, "changed"),
                TrackedFile.Backup => (BackupPath, "replaced"),
                TrackedFile.Payload => (PayloadPath, "altered"),
                _ => throw new ArgumentOutOfRangeException(nameof(trackedFile), trackedFile, null),
            };

            File.WriteAllText(path, contents);
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
