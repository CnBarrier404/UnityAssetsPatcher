using System.Text;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ModPackageTests
{
    private const long SixGiB = 6L * 1024L * 1024L * 1024L;

    [Fact]
    public void Open_WhenOptionalGroupSelectedWithDifferentCase_ReportsCanonicalAppliedGroupName()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        TestManifest.WriteZip(
            zipPath,
            """
            {
              "patches": [
                {
                  "target": "sharedassets0.assets",
                  "type": "AudioClip",
                  "include": [ { "m_Name": "Clip A" } ]
                }
              ],
              "optional": [
                {
                  "name": "Bonus camera",
                  "description": "Adds a camera payload.",
                  "copyFiles": [ { "source": "payload/camera.resource" } ]
                }
              ]
            }
            """);

        try
        {
            using ModPackage package = ModPackage.Open(
                zipPath,
                ["bonus CAMERA"],
                new ModManifestReader(),
                new StepTimer());

            Assert.Equal(["Bonus camera"], package.AppliedOptionalGroups);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Fact]
    public void Open_WhenSourceAssetEntriesExceedTotalExtractionLimit_Throws()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        WriteZip64Package(
            zipPath,
            TestManifest.CreateJson(
                """
                {
                  "patches": [
                    {
                      "target": "sharedassets0.assets",
                      "type": "AudioClip",
                      "include": [ { "m_Name": "Clip A" } ],
                      "replaceFrom": { "assets": "resources/a.assets", "match": "m_Name" }
                    },
                    {
                      "target": "sharedassets0.assets",
                      "type": "AudioClip",
                      "include": [ { "m_Name": "Clip B" } ],
                      "replaceFrom": { "assets": "resources/b.assets", "match": "m_Name" }
                    }
                  ]
                }
                """),
            [
                new FakeZip64Entry("resources/a.assets", SixGiB),
                new FakeZip64Entry("resources/b.assets", SixGiB),
            ]);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                ModPackage.Open(
                    zipPath,
                    [],
                    new ModManifestReader(),
                    new StepTimer()));

            Assert.Contains("Zip package exceeds the maximum allowed total uncompressed size", exception.Message);
            Assert.Contains("resources/b.assets", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Fact]
    public void CopyPayloadFile_WhenPreviousPatchSourceExtractionConsumesLimit_Throws()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string destinationDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string destinationPath = Path.Combine(destinationDirectory, "payload.resource");

        WriteZip64Package(
            zipPath,
            TestManifest.CreateJson(
                """
                {
                  "patches": [
                    {
                      "target": "sharedassets0.assets",
                      "type": "AudioClip",
                      "include": [ { "m_Name": "Clip A" } ],
                      "replaceFrom": { "assets": "resources/a.assets", "match": "m_Name" }
                    }
                  ]
                }
                """),
            [
                new FakeZip64Entry("resources/a.assets", SixGiB),
                new FakeZip64Entry("resources/payload.resource", SixGiB),
            ]);

        try
        {
            using ModPackage package = ModPackage.Open(
                zipPath,
                [],
                new ModManifestReader(),
                new StepTimer());

            var exception = Assert.Throws<InvalidOperationException>(() =>
                package.CopyPayloadFile("resources/payload.resource", destinationPath));

            Assert.Contains("Zip package exceeds the maximum allowed total uncompressed size", exception.Message);
            Assert.Contains("resources/payload.resource", exception.Message);
            Assert.False(File.Exists(destinationPath));
        }
        finally
        {
            File.Delete(zipPath);

            if (Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, recursive: true);
            }
        }
    }

    private static void WriteZip64Package(
        string zipPath,
        string manifestJson,
        IReadOnlyList<FakeZip64Entry> fakeEntries)
    {
        using FileStream stream = File.Create(zipPath);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var centralDirectoryEntries = new List<CentralDirectoryEntry>();

        WriteStoredEntry(writer, centralDirectoryEntries, "Mod/manifest.json", Encoding.UTF8.GetBytes(manifestJson));

        foreach (FakeZip64Entry entry in fakeEntries)
        {
            WriteFakeZip64Entry(writer, centralDirectoryEntries, entry.Name, entry.DeclaredLength);
        }

        long centralDirectoryOffset = stream.Position;

        foreach (CentralDirectoryEntry entry in centralDirectoryEntries)
        {
            WriteCentralDirectoryEntry(writer, entry);
        }

        long centralDirectorySize = stream.Position - centralDirectoryOffset;
        WriteEndOfCentralDirectory(writer, centralDirectoryEntries.Count, centralDirectorySize, centralDirectoryOffset);
    }

    private static void WriteStoredEntry(
        BinaryWriter writer,
        ICollection<CentralDirectoryEntry> centralDirectoryEntries,
        string name,
        byte[] contents)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        long localHeaderOffset = writer.BaseStream.Position;
        uint crc32 = Crc32(contents);

        writer.Write(0x04034b50u);
        writer.Write((ushort)20);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(crc32);
        writer.Write((uint)contents.Length);
        writer.Write((uint)contents.Length);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)0);
        writer.Write(nameBytes);
        writer.Write(contents);

        centralDirectoryEntries.Add(new CentralDirectoryEntry(
            name,
            contents.Length,
            contents.Length,
            crc32,
            localHeaderOffset,
            IsZip64: false));
    }

    private static void WriteFakeZip64Entry(
        BinaryWriter writer,
        ICollection<CentralDirectoryEntry> centralDirectoryEntries,
        string name,
        long declaredLength)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        long localHeaderOffset = writer.BaseStream.Position;
        byte[] zip64Extra = CreateZip64SizeExtra(declaredLength, compressedSize: 0);

        writer.Write(0x04034b50u);
        writer.Write((ushort)45);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(uint.MaxValue);
        writer.Write(uint.MaxValue);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)zip64Extra.Length);
        writer.Write(nameBytes);
        writer.Write(zip64Extra);

        centralDirectoryEntries.Add(new CentralDirectoryEntry(
            name,
            declaredLength,
            0,
            Crc32: 0,
            localHeaderOffset,
            IsZip64: true));
    }

    private static void WriteCentralDirectoryEntry(BinaryWriter writer, CentralDirectoryEntry entry)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Name);
        byte[] extra = entry.IsZip64
            ? CreateZip64SizeExtra(entry.UncompressedSize, entry.CompressedSize)
            : [];

        writer.Write(0x02014b50u);
        writer.Write((ushort)(entry.IsZip64 ? 45 : 20));
        writer.Write((ushort)(entry.IsZip64 ? 45 : 20));
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(entry.Crc32);
        writer.Write(entry.IsZip64 ? uint.MaxValue : (uint)entry.CompressedSize);
        writer.Write(entry.IsZip64 ? uint.MaxValue : (uint)entry.UncompressedSize);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)extra.Length);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write((uint)entry.LocalHeaderOffset);
        writer.Write(nameBytes);
        writer.Write(extra);
    }

    private static void WriteEndOfCentralDirectory(
        BinaryWriter writer,
        int entryCount,
        long centralDirectorySize,
        long centralDirectoryOffset)
    {
        writer.Write(0x06054b50u);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)entryCount);
        writer.Write((ushort)entryCount);
        writer.Write((uint)centralDirectorySize);
        writer.Write((uint)centralDirectoryOffset);
        writer.Write((ushort)0);
    }

    private static byte[] CreateZip64SizeExtra(long uncompressedSize, long compressedSize)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x0001);
        writer.Write((ushort)16);
        writer.Write((ulong)uncompressedSize);
        writer.Write((ulong)compressedSize);

        return stream.ToArray();
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xffffffffu;

        foreach (byte value in bytes)
        {
            crc ^= value;

            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 0
                    ? crc >> 1
                    : (crc >> 1) ^ 0xedb88320u;
            }
        }

        return ~crc;
    }

    private sealed record FakeZip64Entry(string Name, long DeclaredLength);

    private sealed record CentralDirectoryEntry(
        string Name,
        long UncompressedSize,
        long CompressedSize,
        uint Crc32,
        long LocalHeaderOffset,
        bool IsZip64);
}
