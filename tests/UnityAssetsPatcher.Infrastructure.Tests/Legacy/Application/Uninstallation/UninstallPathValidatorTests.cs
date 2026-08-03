using UnityAssetsPatcher.Application.Backups;
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
}
