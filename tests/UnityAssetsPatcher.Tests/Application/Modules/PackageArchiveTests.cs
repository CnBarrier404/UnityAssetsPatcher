using System.IO.Compression;
using UnityAssetsPatcher.Application;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Modules;

public sealed class PackageArchiveTests
{
    [Fact]
    public void CopyEntryToNewFile_WhenEntryLengthExceedsLimit_ThrowsWithoutCreatingDestination()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string destinationPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.resource");

        try
        {
            using (ZipArchive createArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry createEntry = createArchive.CreateEntry("resources/modassets.resource");
                using StreamWriter writer = new(createEntry.Open());
                writer.Write("payload");
            }

            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry oversizedEntry = archive.GetEntry("resources/modassets.resource")!;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                PackageArchive.CopyEntryToNewFile(oversizedEntry, destinationPath, maxEntryBytes: 3));

            Assert.Contains("exceeds the maximum allowed uncompressed size", exception.Message);
            Assert.Contains("resources/modassets.resource", exception.Message);
            Assert.False(File.Exists(destinationPath));
        }
        finally
        {
            File.Delete(zipPath);
            File.Delete(destinationPath);
        }
    }

    [Fact]
    public void CopyEntryToNewFile_WhenEntryIsWithinLimit_CopiesEntry()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string destinationPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.resource");

        try
        {
            using (ZipArchive createArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry createEntry = createArchive.CreateEntry("resources/modassets.resource");
                using StreamWriter writer = new(createEntry.Open());
                writer.Write("payload");
            }

            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry entry = archive.GetEntry("resources/modassets.resource")!;

            PackageArchive.CopyEntryToNewFile(entry, destinationPath, maxEntryBytes: 1024);

            Assert.Equal("payload", File.ReadAllText(destinationPath));
        }
        finally
        {
            File.Delete(zipPath);
            File.Delete(destinationPath);
        }
    }
}
