using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules.Installation;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Modules.Installation;

public sealed class InstallRecordStoreTests
{
    [Fact]
    public void CreateInstall_ReturnsInstallDirectoryAndAssetsBackupDirectory()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "name": "Test Mod",
              "version": "1.0.0",
              "author": "UnityAssetsPatcher.Tests",
              "patches": [
                {
                  "target": "sharedassets0.assets",
                  "type": "Camera",
                  "include": [
                    {
                      "field of view": 90.0
                    }
                  ],
                  "set": [
                    {
                      "field": "field of view",
                      "from": 90.0,
                      "to": 75.0
                    }
                  ]
                }
              ]
            }
            """);

        try
        {
            using ModPackage package = ModPackage.Open(zipPath, [], new ModManifestReader(), new StepTimer());
            var store = new InstallRecordStore(backupDirectory);

            InstallRecordPaths paths = store.CreateInstall(package);

            Assert.StartsWith(backupDirectory, paths.InstallDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Path.Combine(paths.InstallDirectory, "assets"), paths.AssetsBackupDirectory);
            Assert.True(Directory.Exists(paths.InstallDirectory));
        }
        finally
        {
            File.Delete(zipPath);
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }
}
