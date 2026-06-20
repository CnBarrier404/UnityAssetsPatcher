using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ModManifestReaderTests
{
    /// <summary>
    /// Verifies that Read rejects manifest.json entries exceeding the 10 MB size limit,
    /// preventing unbounded memory allocation from maliciously crafted zip files.
    /// </summary>
    [Fact]
    public void Read_WhenZipManifestExceedsMaxSize_ThrowsInvalidOperationException()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        try
        {
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("manifest.json");

                using Stream entryStream = entry.Open();
                const int size = 10 * 1024 * 1024 + 1;
                entryStream.Write(new byte[size]);
            }

            var exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new ModManifestLoader().Load(zipPath));

            Assert.Contains("exceeds maximum allowed size", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    /// Verifies that Read accepts manifest.json entries within the 10 MB size limit.
    /// </summary>
    [Fact]
    public void Read_WhenZipManifestWithinSizeLimit_ReturnsRootElement()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        try
        {
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("manifest.json");

                using StreamWriter writer = new(entry.Open());
                writer.Write(
                    """
                    {
                      "name": "Test Mod",
                      "author": "Tester",
                      "version": "1.0.0",
                      "targets": [
                        {
                          "file": "globalgamemanagers.assets",
                          "patches": [
                            {
                              "type": "GameObject",
                              "match": {
                                "m_Name": "Camera"
                              },
                              "set": {
                                "m_IsActive": {
                                  "from": false,
                                  "to": true
                                }
                              }
                            }
                          ]
                        }
                      ]
                    }
                    """);
            }

            ModManifest manifest = new ModManifestLoader().Load(zipPath);

            Assert.Equal("Test Mod", manifest.Name);
            Assert.Equal("1.0.0", manifest.Version);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}
