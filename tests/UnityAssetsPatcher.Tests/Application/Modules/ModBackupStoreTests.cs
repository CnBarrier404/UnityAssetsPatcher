using System.Text.RegularExpressions;
using UnityAssetsPatcher.Application;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Modules;

public sealed class ModBackupStoreTests
{
    [Fact]
    public void CreateInstallDirectory_RemovesWhitespaceAndInvalidCharactersFromModNameAndVersion()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var store = new ModBackupStore(
            backupDirectory,
            () => new DateTimeOffset(2026, 6, 18, 14, 30, 22, TimeSpan.Zero));

        try
        {
            string installDirectory = store.CreateInstallDirectory("Better: Audio / Pack", "v1 beta");

            Assert.Equal(
                Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-v1beta"),
                installDirectory);
            Assert.True(Directory.Exists(installDirectory));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    [Fact]
    public void CreateInstallDirectory_WhenNameCollides_AppendsUniqueSuffix()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var store = new ModBackupStore(
            backupDirectory,
            () => new DateTimeOffset(2026, 6, 18, 14, 30, 22, TimeSpan.Zero));

        try
        {
            string first = store.CreateInstallDirectory("Better Audio Pack", "1.0.0");
            string second = store.CreateInstallDirectory("Better Audio Pack", "1.0.0");

            Assert.Matches(
                Regex.Escape(Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0")) + "$",
                first);
            Assert.Matches(
                Regex.Escape(Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0.1")) + "$",
                second);
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    [Fact]
    public void BackupFile_CopiesSourceWithOriginalFileName()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");

        try
        {
            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(sourcePath, "patched");

            string path = ModBackupStore.BackupFile(sourcePath, backupDirectory);

            Assert.Equal(Path.Combine(backupDirectory, Path.GetFileName(sourcePath)), path);
            Assert.Equal("patched", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(sourcePath);
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    [Fact]
    public void RestoreFile_ReplacesAssetsFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string backupPath = Path.Combine(directory, "backup.assets");
        string assetsFilePath = Path.Combine(directory, "sharedassets0.assets");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(backupPath, "original");
            File.WriteAllText(assetsFilePath, "patched");

            ModBackupStore.RestoreFile(backupPath, assetsFilePath);

            Assert.Equal("original", File.ReadAllText(assetsFilePath));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void DeleteRecord_RemovesInstallDirectoryFromInstalledList()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string installDirectory = Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0");
        var record = new InstallRecord(
            "install-1",
            DateTimeOffset.Parse("2026-06-18T14:30:22Z"),
            "Better Audio Pack",
            "1.0.0",
            "UnityAssetsPatcher.Tests",
            null,
            @"C:\Games\Example",
            [],
            []);
        var store = new ModBackupStore(backupDirectory);

        try
        {
            store.Save(record, installDirectory);

            ModBackupStore.DeleteRecord(installDirectory);

            Assert.False(File.Exists(Path.Combine(installDirectory, "record.json")));
            Assert.DoesNotContain(
                store.ListInstalled(),
                summary => summary.InstallDirectory == installDirectory);
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }
}
