using System.IO.Compression;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Mods;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Mods;

public sealed class ModPackageReaderTests
{
    [Fact]
    public void Open_WhenManifestIsMissing_ThrowsInvalidDataException()
    {
        byte[] archiveBytes = CreateArchive(("payload.bin", [1]));

        InvalidDataException exception = OpenFailure(archiveBytes);

        Assert.Contains("does not contain a manifest.json file", exception.Message);
    }

    [Fact]
    public void Open_WhenMultipleManifestsExist_ThrowsInvalidDataException()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Nested/MANIFEST.JSON", "{}"u8.ToArray()));

        InvalidDataException exception = OpenFailure(archiveBytes);

        Assert.Contains("contains multiple manifest.json files", exception.Message);
    }

    [Fact]
    public void Open_WhenEntriesCollideIgnoringCase_ThrowsInvalidDataException()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Payload/file.bin", [1]),
            ("payload/FILE.BIN", [2]));

        InvalidDataException exception = OpenFailure(archiveBytes);

        Assert.Contains("duplicate entry", exception.Message);
        Assert.Contains("payload/FILE.BIN", exception.Message);
    }

    [Theory]
    [InlineData("../payload.bin")]
    [InlineData("/payload.bin")]
    [InlineData("payload/./file.bin")]
    [InlineData("payload//file.bin")]
    [InlineData("C:/payload.bin")]
    [InlineData("payload/file.bin.")]
    public void Open_WhenEntryPathIsUnsafe_ThrowsInvalidDataException(string entryPath)
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            (entryPath, [1]));

        InvalidDataException exception = OpenFailure(archiveBytes);

        Assert.Contains("entry path is unsafe", exception.Message);
        Assert.Contains(entryPath, exception.Message);
    }

    [Fact]
    public void ReadManifest_WhenManifestExceedsLimit_ThrowsInvalidDataException()
    {
        byte[] manifest = new byte[10L * 1024L * 1024L + 1];
        byte[] archiveBytes = CreateArchive(("manifest.json", manifest));
        using IModPackageSession session = OpenPackage(new StubFileSystemOperations(archiveBytes));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => session.ReadManifest());

        Assert.Contains("10485760-byte limit", exception.Message);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenEntryExists_WritesThroughAtomicFileSystemBoundary()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("payload.bin", [1, 2, 3]));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        using IModPackageSession session = OpenPackage(fileSystem);

        long copiedBytes = session.CopyEntryToNewFile(
            "PAYLOAD.BIN",
            "payload.output",
            TestContext.Current.CancellationToken);

        Assert.Equal(3, copiedBytes);
        Assert.Equal([1, 2, 3], Assert.Single(fileSystem.WrittenFiles).Value);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenEntryIsMissing_ThrowsInvalidDataException()
    {
        byte[] archiveBytes = CreateArchive(("manifest.json", "{}"u8.ToArray()));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        using IModPackageSession session = OpenPackage(fileSystem);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => session.CopyEntryToNewFile(
            "missing.bin",
            "payload.output",
            TestContext.Current.CancellationToken));

        Assert.Contains("entry was not found: missing.bin", exception.Message);
        Assert.Empty(fileSystem.WrittenFiles);
    }

    [Fact]
    public void Open_WhenFileSystemFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var reader = new ModPackageReader(new StubFileSystemOperations(expected));

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => reader.Open("missing.zip"));

        Assert.Same(expected, exception);
    }

    private static InvalidDataException OpenFailure(byte[] archiveBytes)
    {
        var reader = new ModPackageReader(new StubFileSystemOperations(archiveBytes));

        return Assert.Throws<InvalidDataException>(() => reader.Open("mod.zip"));
    }

    private static IModPackageSession OpenPackage(IFileSystemOperations fileSystemOperations)
    {
        var reader = new ModPackageReader(fileSystemOperations);

        return reader.Open("mod.zip");
    }

    private static byte[] CreateArchive(params (string Path, byte[] Contents)[] entries)
    {
        using var output = new MemoryStream();

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string path, byte[] contents) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
                using Stream stream = entry.Open();

                stream.Write(contents);
            }
        }

        return output.ToArray();
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        private readonly byte[]? _archiveBytes;
        private readonly Exception? _openException;

        public Dictionary<string, byte[]> WrittenFiles { get; } = new(StringComparer.Ordinal);

        public StubFileSystemOperations(byte[] archiveBytes)
        {
            _archiveBytes = archiveBytes;
        }

        public StubFileSystemOperations(Exception openException)
        {
            _openException = openException;
        }

        public Stream OpenRead(string path)
        {
            if (_openException is not null)
            {
                throw _openException;
            }

            return new MemoryStream(_archiveBytes!, writable: false);
        }

        public FileIntegrity ComputeFileIntegrity(string path)
        {
            throw new NotSupportedException();
        }

        public FileAttributes GetAttributes(string path)
        {
            throw new NotSupportedException();
        }

        public void WriteFileAtomically(string destinationPath, FileDestinationMode mode, Action<Stream> writer)
        {
            using var output = new MemoryStream();

            writer(output);
            WrittenFiles.Add(Path.GetFullPath(destinationPath), output.ToArray());
        }

        public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode)
        {
            throw new NotSupportedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotSupportedException();
        }

        public void EnsureDirectory(string path) { }

        public void MoveDirectory(string sourcePath, string destinationPath)
        {
            throw new NotSupportedException();
        }

        public void DeleteDirectoryTree(string path)
        {
            throw new NotSupportedException();
        }
    }
}
