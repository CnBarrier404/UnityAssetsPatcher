using System.IO.Compression;
using UnityAssetsPatcher.Application.Manifests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ManifestJsonReaderTests
{
    /// <summary>
    /// Verifies that ReadManifestElementFromZip rejects manifest.json entries exceeding the 10 MB size limit,
    /// preventing unbounded memory allocation from maliciously crafted zip files.
    /// </summary>
    [Fact]
    public void ReadManifestElementFromZip_WhenManifestExceedsMaxSize_ThrowsInvalidOperationException()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        try
        {
            // Create a zip with a manifest.json slightly over 10 MB
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("manifest.json");

                using Stream entryStream = entry.Open();
                const int size = 10 * 1024 * 1024 + 1;
                entryStream.Write(new byte[size]);
            }

            using ZipArchive zip = ZipFile.OpenRead(zipPath);

            var exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ManifestJsonReader.ReadManifestElementFromZip(zip, zipPath));

            Assert.Contains("exceeds maximum allowed size", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    /// Verifies that ReadManifestElementFromZip accepts manifest.json entries within the 10 MB size limit.
    /// </summary>
    [Fact]
    public void ReadManifestElementFromZip_WhenManifestWithinSizeLimit_ReturnsRootElement()
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
                      "version": "1.0.0"
                    }
                    """);
            }

            using ZipArchive zip = ZipFile.OpenRead(zipPath);

            var element = ManifestJsonReader.ReadManifestElementFromZip(zip, zipPath);

            Assert.Equal("Test Mod", element.GetProperty("name").GetString());
            Assert.Equal("1.0.0", element.GetProperty("version").GetString());
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}
