using System.Text.Json;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class UninstallModWorkflowTests
{
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
            "install-1",
            InstallRecordStatus.Installed,
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            null,
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            gameDirectory,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    targetPath,
                    backupPath,
                    null,
                    1,
                    1),
            ],
            []);
        var store = new ModInstallationStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = new UninstallModWorkflow(store);

        try
        {
            UninstallPreviewResult result = workflow.Preview(new UninstallPreviewRequest(installDirectory));

            Assert.False(result.CanUninstall);
            UninstallPreviewRestoredFileResult file = Assert.Single(result.RestoredFiles);
            Assert.True(file.TargetExists);
            Assert.False(file.BackupExists);
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
    public void Uninstall_WhenRecordIsInstalled_RestoresAssetsDeletesPayloadAndMarksRecordUninstalled()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        string installAssetsDirectory = Path.Combine(installDirectory, "assets");
        string uninstallDirectory = Path.Combine(installDirectory, "uninstall");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupPath = Path.Combine(installAssetsDirectory, "sharedassets0.assets");
        string payloadPath = Path.Combine(targetDirectory, "modassets.resource");
        string recordPath = Path.Combine(installDirectory, "record.json");
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(installAssetsDirectory);
        File.WriteAllText(targetPath, "patched");
        File.WriteAllText(backupPath, "original");
        File.WriteAllText(payloadPath, "payload");

        var record = new InstallRecord(
            "install-1",
            InstallRecordStatus.Installed,
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            null,
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            gameDirectory,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    targetPath,
                    backupPath,
                    null,
                    1,
                    1),
            ],
            [
                new InstallRecordCopiedFile("resources/modassets.resource", payloadPath, true),
            ]);
        var store = new ModInstallationStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = new UninstallModWorkflow(store);

        try
        {
            UninstallModResult result = workflow.Uninstall(new UninstallModRequest(installDirectory));

            Assert.Equal("Better Audio Pack", result.ModName);
            Assert.Equal("original", File.ReadAllText(targetPath));
            Assert.False(File.Exists(payloadPath));
            string uninstallBackup = Assert.Single(result.RestoredFiles).UninstallBackupPath;
            Assert.StartsWith(uninstallDirectory, uninstallBackup, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("patched", File.ReadAllText(uninstallBackup));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(recordPath));
            Assert.Equal("uninstalled", document.RootElement.GetProperty("status").GetString());
            Assert.True(document.RootElement.TryGetProperty("uninstalledAt", out JsonElement uninstalledAt));
            Assert.False(string.IsNullOrWhiteSpace(uninstalledAt.GetString()));
            Assert.Equal(
                uninstallBackup,
                document.RootElement.GetProperty("patchedFiles")[0].GetProperty("uninstallBackupPath").GetString());
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
            "install-1",
            InstallRecordStatus.Installed,
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            null,
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            gameDirectory,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    targetPath,
                    backupPath,
                    null,
                    1,
                    1),
            ],
            []);
        var store = new ModInstallationStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = new UninstallModWorkflow(store);

        try
        {
            File.Delete(targetPath);

            var exception = Assert.Throws<FileNotFoundException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory)));

            Assert.Contains("Assets file was deleted during uninstall", exception.Message);
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
            "install-1",
            InstallRecordStatus.Installed,
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            null,
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            gameDirectory,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    targetPath,
                    backupPath,
                    null,
                    1,
                    1),
            ],
            []);
        var store = new ModInstallationStore(backupDirectory);
        store.Save(record, installDirectory);
        var workflow = new UninstallModWorkflow(store);

        try
        {
            File.Delete(backupPath);

            var exception = Assert.Throws<FileNotFoundException>(() =>
                workflow.Uninstall(new UninstallModRequest(installDirectory)));

            Assert.Contains("Backup file was deleted during uninstall", exception.Message);
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
}
