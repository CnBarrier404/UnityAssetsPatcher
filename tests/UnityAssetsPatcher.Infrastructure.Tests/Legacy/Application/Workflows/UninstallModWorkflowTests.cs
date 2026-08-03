using System.Text.Json;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Tests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class UninstallModWorkflowTests
{
    [Fact]
    public void Preview_WhenRecordHasGameName_AutoResolvesTrustedSteamDirectory()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string steamRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(steamRoot, "steamapps", "common", "ExampleGame");
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installDirectory, "assets", "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.WriteAllText(Path.Combine(steamRoot, "steamapps", "appmanifest_1.acf"),
            "\"name\" \"Example Game\"\n\"installdir\" \"ExampleGame\"");
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            "Example Game",
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, targetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(targetPath),
                    FileIntegrity.Create(backupPath))
            ],
            []);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store, TestDependencies.CreateGameDirectoryResolver([steamRoot]));

        try
        {
            UninstallPreviewResult result = workflow.Preview(new UninstallPreviewRequest(record.Id));

            Assert.Equal(Path.GetFullPath(gameDirectory), result.GameDirectory);
            Assert.Equal(record.Id, result.InstallId);
            Assert.True(result.CanUninstall);
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(steamRoot))
            {
                Directory.Delete(steamRoot, true);
            }
        }
    }

    [Fact]
    public void Preview_WhenInstallBackupIsMissing_ReportsBlockingRestoreIssue()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installDirectory, "assets", "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "patched");
        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, targetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(targetPath),
                    TextIntegrity("original")),
            ],
            []);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            UninstallPreviewResult result =
                workflow.Preview(new UninstallPreviewRequest(record.Id, gameDirectory));

            Assert.False(result.CanUninstall);
            UninstallPreviewRestoredFileResult file = Assert.Single(result.RestoredFiles);
            Assert.Equal(FileIntegrityStatus.Matches, file.TargetStatus);
            Assert.Equal(FileIntegrityStatus.Missing, file.BackupStatus);
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenRecordIsInstalled_RestoresAssetsDeletesPayloadAndDeletesInstallDirectory()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string payloadPath = Path.Combine(targetDirectory, "modassets.resource");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");
        File.WriteAllText(payloadPath, "payload");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, targetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(targetPath),
                    FileIntegrity.Create(backupPath)),
            ],
            [
                new InstallRecordCopiedFile("resources/modassets.resource", RelativeToGame(gameDirectory, payloadPath),
                    FileIntegrity.Create(payloadPath)),
            ]);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            UninstallModResult result = workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory));

            Assert.Equal("Better Audio Pack", result.ModName);
            Assert.Equal("original", File.ReadAllText(targetPath));
            Assert.False(File.Exists(payloadPath));
            Assert.Single(result.RestoredFiles);
            Assert.False(Directory.Exists(installDirectory));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenInstallDirectoryCannotMove_KeepsRecordInstalled()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string lockedPath = Path.Combine(installDirectory, "locked.tmp");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");
        File.WriteAllText(lockedPath, "locked");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, targetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(targetPath),
                    FileIntegrity.Create(backupPath)),
            ],
            []);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            lockedPath = Path.Combine(installDirectory, "locked.tmp");
            using FileStream _ = File.Open(
                lockedPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            Assert.ThrowsAny<Exception>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.True(File.Exists(Path.Combine(installDirectory, "record.json")));
            Assert.Contains(
                workflow.ListInstalled(),
                summary => summary.InstallId == record.Id);
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenPayloadFileIsLocked_DoesNotRestoreAssetsOrDeleteRecord()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string payloadPath = Path.Combine(targetDirectory, "modassets.resource");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");
        File.WriteAllText(payloadPath, "payload");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, targetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(targetPath),
                    FileIntegrity.Create(backupPath)),
            ],
            [
                new InstallRecordCopiedFile("resources/modassets.resource", RelativeToGame(gameDirectory, payloadPath),
                    FileIntegrity.Create(payloadPath)),
            ]);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            using FileStream _ = File.Open(
                payloadPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            Assert.ThrowsAny<Exception>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Equal("patched", File.ReadAllText(targetPath));
            Assert.True(File.Exists(Path.Combine(installDirectory, "record.json")));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenLaterBackupFileIsMissing_DoesNotRestoreEarlierAssetsFile()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string firstTargetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string firstBackupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string secondTargetPath = Path.Combine(targetDirectory, "sharedassets1.assets");
        string secondBackupPath = Path.Combine(installAssetsDirectory, "sharedassets1.assets");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(firstTargetPath, "first patched");
        File.WriteAllText(firstBackupPath, "first original");
        File.WriteAllText(secondTargetPath, "second patched");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, firstTargetPath),
                    RelativeToInstall(installDirectory, firstBackupPath),
                    1,
                    1,
                    FileIntegrity.Create(firstTargetPath),
                    FileIntegrity.Create(firstBackupPath)),
                new InstallRecordPatchedFile(
                    "sharedassets1.assets",
                    RelativeToGame(gameDirectory, secondTargetPath),
                    RelativeToInstall(installDirectory, secondBackupPath),
                    1,
                    1,
                    FileIntegrity.Create(secondTargetPath),
                    TextIntegrity("second original")),
            ],
            []);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Contains("backup file is missing", exception.Message);
            Assert.Equal("first patched", File.ReadAllText(firstTargetPath));
            Assert.Equal("second patched", File.ReadAllText(secondTargetPath));
            Assert.True(Directory.Exists(installDirectory));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenLaterAssetsFileIsMissing_DoesNotRestoreEarlierAssetsFile()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string firstTargetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string firstBackupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string secondTargetPath = Path.Combine(targetDirectory, "sharedassets1.assets");
        string secondBackupPath = Path.Combine(installAssetsDirectory, "sharedassets1.assets");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(firstTargetPath, "first patched");
        File.WriteAllText(firstBackupPath, "first original");
        File.WriteAllText(secondBackupPath, "second original");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, firstTargetPath),
                    RelativeToInstall(installDirectory, firstBackupPath),
                    1,
                    1,
                    FileIntegrity.Create(firstTargetPath),
                    FileIntegrity.Create(firstBackupPath)),
                new InstallRecordPatchedFile(
                    "sharedassets1.assets",
                    RelativeToGame(gameDirectory, secondTargetPath),
                    RelativeToInstall(installDirectory, secondBackupPath),
                    1,
                    1,
                    TextIntegrity("second patched"),
                    FileIntegrity.Create(secondBackupPath)),
            ],
            []);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Contains("assets file is missing", exception.Message);
            Assert.Equal("first patched", File.ReadAllText(firstTargetPath));
            Assert.True(Directory.Exists(installDirectory));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenLaterAssetsFileIsLocked_RollsBackEarlierRestoredAssetsFile()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string firstTargetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string firstBackupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string secondTargetPath = Path.Combine(targetDirectory, "sharedassets1.assets");
        string secondBackupPath = Path.Combine(installAssetsDirectory, "sharedassets1.assets");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(firstTargetPath, "first patched");
        File.WriteAllText(firstBackupPath, "first original");
        File.WriteAllText(secondTargetPath, "second patched");
        File.WriteAllText(secondBackupPath, "second original");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, firstTargetPath),
                    RelativeToInstall(installDirectory, firstBackupPath),
                    1,
                    1,
                    FileIntegrity.Create(firstTargetPath),
                    FileIntegrity.Create(firstBackupPath)),
                new InstallRecordPatchedFile(
                    "sharedassets1.assets",
                    RelativeToGame(gameDirectory, secondTargetPath),
                    RelativeToInstall(installDirectory, secondBackupPath),
                    1,
                    1,
                    FileIntegrity.Create(secondTargetPath),
                    FileIntegrity.Create(secondBackupPath)),
            ],
            []);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            using (FileStream _ = File.Open(
                       secondTargetPath,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                Assert.ThrowsAny<Exception>(() =>
                    workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));
            }

            Assert.Equal("first patched", File.ReadAllText(firstTargetPath));
            Assert.Equal("second patched", File.ReadAllText(secondTargetPath));
            Assert.True(Directory.Exists(installDirectory));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenInjectedRestoreFails_AutomaticRecoveryRestoresEarlierFile()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string firstTargetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string firstBackupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string secondTargetPath = Path.Combine(targetDirectory, "sharedassets1.assets");
        string secondBackupPath = Path.Combine(installAssetsDirectory, "sharedassets1.assets");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(firstTargetPath, "first patched");
        File.WriteAllText(firstBackupPath, "first original");
        File.WriteAllText(secondTargetPath, "second patched");
        File.WriteAllText(secondBackupPath, "second original");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, firstTargetPath),
                    RelativeToInstall(installDirectory, firstBackupPath),
                    1,
                    1,
                    FileIntegrity.Create(firstTargetPath),
                    FileIntegrity.Create(firstBackupPath)),
                new InstallRecordPatchedFile(
                    "sharedassets1.assets",
                    RelativeToGame(gameDirectory, secondTargetPath),
                    RelativeToInstall(installDirectory, secondBackupPath),
                    1,
                    1,
                    FileIntegrity.Create(secondTargetPath),
                    FileIntegrity.Create(secondBackupPath)),
            ],
            []);

        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        int firstFileRestoreAttempts = 0;
        IFileSystemOperations fileSystemOperations = TestDependencies.FileSystemOperations;
        var executor = new UninstallExecutor(
            store,
            fileSystemOperations,
            (backupPath, targetPath) =>
            {
                if (targetPath == firstTargetPath && firstFileRestoreAttempts++ == 0)
                {
                    fileSystemOperations.CopyFile(backupPath, targetPath);

                    return;
                }

                throw new IOException($"Simulated restore failure for: {targetPath}");
            });
        var workflow = CreateWorkflow(store, executor: executor);

        try
        {
            Assert.Throws<IOException>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Equal("first patched", File.ReadAllText(firstTargetPath));
            Assert.Equal("second patched", File.ReadAllText(secondTargetPath));
            Assert.False(Directory.Exists(store.TransactionDirectory));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Preview_WhenInstallDirectoryEscapesBackupDirectory_ThrowsPathSafetyError()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string escapedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var store = CreateBackupRepository(backupDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            Directory.CreateDirectory(escapedDirectory);

            var exception = Assert.Throws<KeyNotFoundException>(() =>
                workflow.Preview(new UninstallPreviewRequest(escapedDirectory)));

            Assert.Contains("Install record not found", exception.Message);
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(escapedDirectory))
            {
                Directory.Delete(escapedDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenBackupPathEscapesInstallDirectory_DoesNotRestoreOrDeleteRecord()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string escapedBackupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(escapedBackupDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(escapedBackupDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "escaped original");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, targetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(targetPath),
                    FileIntegrity.Create(backupPath)),
            ],
            []);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitInvalidRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<BackupRecoveryException>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Contains("backup file path is not trusted", exception.Message);
            Assert.Equal("patched", File.ReadAllText(targetPath));
            Assert.True(File.Exists(Path.Combine(installDirectory, "record.json")));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }

            if (Directory.Exists(escapedBackupDirectory))
            {
                Directory.Delete(escapedBackupDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenAssetsPathEscapesGameDirectory_DoesNotRestoreOrDeleteRecord()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string escapedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string escapedTargetPath = Path.Combine(escapedDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        Directory.CreateDirectory(escapedDirectory);
        File.WriteAllText(escapedTargetPath, "victim");
        File.WriteAllText(backupPath, "original");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, escapedTargetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(escapedTargetPath),
                    FileIntegrity.Create(backupPath)),
            ],
            []);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitInvalidRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<BackupRecoveryException>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Contains("assets file path is not trusted", exception.Message);
            Assert.Equal("victim", File.ReadAllText(escapedTargetPath));
            Assert.True(File.Exists(Path.Combine(installDirectory, "record.json")));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }

            if (Directory.Exists(escapedDirectory))
            {
                Directory.Delete(escapedDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenPayloadDestinationEscapesGameDirectory_DoesNotDeleteFileOrDeleteRecord()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string escapedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string escapedPayloadPath = Path.Combine(escapedDirectory, "modassets.resource");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        Directory.CreateDirectory(escapedDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");
        File.WriteAllText(escapedPayloadPath, "victim payload");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, targetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(targetPath),
                    FileIntegrity.Create(backupPath)),
            ],
            [
                new InstallRecordCopiedFile("resources/modassets.resource",
                    RelativeToGame(gameDirectory, escapedPayloadPath),
                    FileIntegrity.Create(escapedPayloadPath)),
            ]);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitInvalidRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<BackupRecoveryException>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Contains("payload file path is not trusted", exception.Message);
            Assert.Equal("victim payload", File.ReadAllText(escapedPayloadPath));
            Assert.True(File.Exists(Path.Combine(installDirectory, "record.json")));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }

            if (Directory.Exists(escapedDirectory))
            {
                Directory.Delete(escapedDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenPayloadDestinationFileNameDoesNotMatchSource_DoesNotDeleteFile()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string payloadPath = Path.Combine(targetDirectory, "renamed.resource");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");
        File.WriteAllText(payloadPath, "victim payload");

        var record = new InstallRecord(
            string.Empty,
            GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
            1,
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    RelativeToGame(gameDirectory, targetPath),
                    RelativeToInstall(installDirectory, backupPath),
                    1,
                    1,
                    FileIntegrity.Create(targetPath),
                    FileIntegrity.Create(backupPath)),
            ],
            [
                new InstallRecordCopiedFile("resources/modassets.resource", RelativeToGame(gameDirectory, payloadPath),
                    FileIntegrity.Create(payloadPath)),
            ]);
        var store = CreateBackupRepository(backupDirectory);
        installDirectory = CommitRecord(store, record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Contains("Payload destination file name must match source file name", exception.Message);
            Assert.Equal("victim payload", File.ReadAllText(payloadPath));
            Assert.True(File.Exists(Path.Combine(installDirectory, "record.json")));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    [Fact]
    public void Uninstall_WhenPayloadDestinationTraversesDirectoryLink_DoesNotDeleteFileOrDeleteRecord()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string escapedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string linkedDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string payloadPath = Path.Combine(linkedDirectory, "modassets.resource");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(escapedDirectory);

        try
        {
            if (!TryCreateDirectorySymbolicLink(linkedDirectory, escapedDirectory, out string? skipReason))
            {
                Assert.Skip(skipReason!);
            }

            File.WriteAllText(payloadPath, "victim payload");
            var record = new InstallRecord(
                string.Empty,
                GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, gameDirectory),
                1,
                "install-1",
                DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
                "Better Audio Pack",
                "1.0.0",
                "UnityAssetsPatcher.Tests",
                null,
                [],
                [
                    new InstallRecordCopiedFile("resources/modassets.resource",
                        RelativeToGame(gameDirectory, payloadPath),
                        FileIntegrity.Create(payloadPath))
                ]);
            var store = CreateBackupRepository(backupDirectory);
            installDirectory = CommitRecord(store, record, installDirectory);
            var workflow = CreateWorkflow(store);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(record.Id, gameDirectory)));

            Assert.Contains("Uninstall payload destination path must be inside its trusted directory",
                exception.Message);
            Assert.Equal("victim payload", File.ReadAllText(payloadPath));
            Assert.True(File.Exists(Path.Combine(installDirectory, "record.json")));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }

            if (Directory.Exists(escapedDirectory))
            {
                Directory.Delete(escapedDirectory, true);
            }
        }
    }

    private static bool TryCreateDirectorySymbolicLink(
        string linkPath,
        string targetPath,
        out string? skipReason)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            skipReason = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skipReason = $"Cannot create directory symbolic link in this environment: {exception.Message}";
            return false;
        }
    }

    private static UninstallModWorkflow CreateWorkflow(
        BackupRepository store,
        GameDirectoryResolver? gameDirectoryResolver = null,
        UninstallExecutor? executor = null)
    {
        return new UninstallModWorkflow(
            new UninstallPlanner(
                store,
                gameDirectoryResolver ?? TestDependencies.CreateGameDirectoryResolver(),
                TestDependencies.FileSystemOperations),
            executor ?? new UninstallExecutor(
                store,
                TestDependencies.FileSystemOperations),
            store);
    }

    private static BackupRepository CreateBackupRepository(string backupDirectory)
    {
        return TestDependencies.CreateBackupRepository(
            backupDirectory,
            TestDependencies.FileSystemOperations);
    }

    private static string RelativeToGame(string gameDirectory, string path)
    {
        return Path.GetRelativePath(gameDirectory, path);
    }

    private static string RelativeToInstall(string installDirectory, string path)
    {
        return Path.GetRelativePath(installDirectory, path);
    }

    private static string CommitRecord(BackupRepository store, InstallRecord record, string preparedDirectory)
    {
        _ = store.LoadMetadata();
        string installDirectory = store.GetInstallDirectory(record.Id);
        Directory.CreateDirectory(preparedDirectory);
        Directory.Move(preparedDirectory, installDirectory);
        TestDependencies.WriteCommittedRecord(store, record, installDirectory);
        return installDirectory;
    }

    private static string CommitInvalidRecord(BackupRepository store, InstallRecord record, string preparedDirectory)
    {
        BackupRepositoryMetadata repository = store.LoadMetadata();
        record = new InstallRecord(
            repository.RepositoryId,
            record.GameInstanceFingerprint,
            record.InstallSequence,
            record.Id,
            record.InstalledAt,
            record.ModName,
            record.ModVersion,
            record.ModAuthor,
            record.GameName,
            record.PatchedFiles,
            record.CopiedFiles,
            record.OptionalGroups);
        string installDirectory = store.GetInstallDirectory(record.Id);
        Directory.CreateDirectory(preparedDirectory);
        Directory.Move(preparedDirectory, installDirectory);
        File.WriteAllText(
            Path.Combine(installDirectory, "record.json"),
            JsonSerializer.Serialize(record, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }));
        return installDirectory;
    }

    private static FileIntegrity TextIntegrity(string contents) =>
        FileIntegrity.Create(System.Text.Encoding.UTF8.GetBytes(contents));
}
