using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Application.Workflows;
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store, new GameDirectoryResolver([steamRoot]));

        try
        {
            UninstallPreviewResult result = workflow.Preview(new UninstallPreviewRequest(installDirectory));

            Assert.Equal(Path.GetFullPath(gameDirectory), result.GameDirectory);
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            UninstallPreviewResult result =
                workflow.Preview(new UninstallPreviewRequest(installDirectory, gameDirectory));

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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            UninstallModResult result = workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory));

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
    public void Uninstall_WhenInstallDirectoryCleanupFails_RemovesRecordFromInstalledList()
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            using FileStream _ = File.Open(
                lockedPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            Assert.ThrowsAny<Exception>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

            Assert.False(File.Exists(Path.Combine(installDirectory, "record.json")));
            Assert.DoesNotContain(
                workflow.ListInstalled(),
                summary => summary.InstallDirectory == installDirectory);
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            using FileStream _ = File.Open(
                payloadPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            Assert.ThrowsAny<Exception>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

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
    public void Uninstall_WhenAssetsFileDeletedDuringUninstall_ThrowsRaceConditionError()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");

        var record = new InstallRecord(
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            File.Delete(targetPath);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

            Assert.Contains("assets file is missing", exception.Message);
            Assert.Contains("sharedassets0.assets", exception.Message);
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
    public void Uninstall_WhenBackupFileDeletedDuringUninstall_ThrowsRaceConditionError()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");

        var record = new InstallRecord(
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            File.Delete(backupPath);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

            Assert.Contains("backup file is missing", exception.Message);
            Assert.Contains("sharedassets0.assets", exception.Message);
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
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
                    workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));
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
    public void Uninstall_WhenRestoreAndRollbackBothFail_ReportsBothFailures()
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
        File.WriteAllText(firstBackupPath, new string('o', 8 * 1024 * 1024));
        File.WriteAllText(secondTargetPath, "second patched");
        File.WriteAllText(secondBackupPath, "second original");

        var record = new InstallRecord(
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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

        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        FileStream? restoreAttemptBackupLock = null;
        FileStream? secondTargetLock = null;
        using var watcher = new FileSystemWatcher(targetDirectory, ".sharedassets0.assets.*.uninstall.tmp");

        watcher.EnableRaisingEvents = true;
        watcher.Created += (_, args) =>
        {
            restoreAttemptBackupLock ??= OpenExclusiveWhenReady(args.FullPath);
            secondTargetLock ??= OpenExclusiveWhenReady(secondTargetPath);
        };

        try
        {
            var exception = Assert.Throws<AggregateException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

            Assert.True(exception.InnerExceptions.Count >= 2);
        }
        finally
        {
            restoreAttemptBackupLock?.Dispose();
            secondTargetLock?.Dispose();

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
        var store = new ModBackupStore(backupDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            Directory.CreateDirectory(escapedDirectory);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Preview(new UninstallPreviewRequest(escapedDirectory)));

            Assert.Contains("Install directory must be inside the backup directory", exception.Message);
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

            Assert.Contains("Invalid uninstall backup path", exception.Message);
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

            Assert.Contains("Invalid uninstall assets file path", exception.Message);
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

            Assert.Contains("Invalid uninstall payload destination path", exception.Message);
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
            InstallRecordValidator.CurrentFormatVersion,
            GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
        var store = new ModBackupStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = CreateWorkflow(store);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

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
                InstallRecordValidator.CurrentFormatVersion,
                GameInstanceIdentity.CreateFingerprint(gameDirectory),
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
            var store = new ModBackupStore(backupDirectory);
            store.Save(record, installDirectory);
            var workflow = CreateWorkflow(store);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory, gameDirectory)));

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

    private static FileStream OpenExclusiveWhenReady(string path)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                return File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(10);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(10);
            }
        }

        return File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private static UninstallModWorkflow CreateWorkflow(
        ModBackupStore store,
        GameDirectoryResolver? gameDirectoryResolver = null)
    {
        return new UninstallModWorkflow(
            new UninstallPlanner(store, gameDirectoryResolver ?? new GameDirectoryResolver([])),
            new UninstallExecutor(),
            store);
    }

    private static string RelativeToGame(string gameDirectory, string path)
    {
        return Path.GetRelativePath(gameDirectory, path);
    }

    private static string RelativeToInstall(string installDirectory, string path)
    {
        return Path.GetRelativePath(installDirectory, path);
    }

    private static FileIntegrity TextIntegrity(string contents) =>
        FileIntegrity.Create(System.Text.Encoding.UTF8.GetBytes(contents));
}
