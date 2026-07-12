using System.IO.Compression;
using UnityAssetsPatcher.Application.Manifests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Manifests;

public sealed class ModManifestJsonReaderTests
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
                    new ModManifestReader().Load(zipPath));

            Assert.Contains("exceeds maximum allowed size", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}
