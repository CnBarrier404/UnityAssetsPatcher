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
    public void Read_WhenManifestIsMissing_ThrowsInvalidDataException()
    {
        byte[] archiveBytes = CreateArchive(("payload.bin", [1]));

        InvalidDataException exception = ReadFailure(archiveBytes);

        Assert.Contains("does not contain a manifest.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WhenMultipleManifestsExist_ThrowsInvalidDataException()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Nested/MANIFEST.JSON", "{}"u8.ToArray()));

        InvalidDataException exception = ReadFailure(archiveBytes);

        Assert.Contains("multiple manifest.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WhenEntriesCollideIgnoringCase_ThrowsInvalidDataException()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Payload/file.bin", [1]),
            ("payload/FILE.BIN", [2]));

        InvalidDataException exception = ReadFailure(archiveBytes);

        Assert.Contains("payload/FILE.BIN", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../payload.bin")]
    [InlineData("/payload.bin")]
    [InlineData("payload/./file.bin")]
    [InlineData("payload//file.bin")]
    [InlineData("C:/payload.bin")]
    [InlineData("payload/file.bin.")]
    public void Read_WhenEntryPathIsUnsafe_ThrowsInvalidDataException(string entryPath)
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            (entryPath, [1]));

        InvalidDataException exception = ReadFailure(archiveBytes);

        Assert.Contains(entryPath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenManifestExceedsLimit_ThrowsInvalidDataException()
    {
        byte[] manifest = new byte[10L * 1024L * 1024L + 1];
        byte[] archiveBytes = CreateArchive(("manifest.json", manifest));
        var reader = new ModPackageReader(new StubFileSystemOperations(archiveBytes));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            reader.ReadManifestAsync("mod.zip", TestContext.Current.CancellationToken));

        Assert.Contains("10485760-byte", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WhenEntriesExist_ExtractsContentAndReturnsPaths()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("payload/file.bin", [1, 2, 3]));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        var reader = new ModPackageReader(fileSystem);

        PackageContent content = reader.Read(
            "mod.zip",
            "extracted",
            TestContext.Current.CancellationToken);

        Assert.Equal("{}"u8.ToArray(), content.Manifest);
        string extractedPath = content.EntryPaths["PAYLOAD/FILE.BIN"];
        Assert.Equal([1, 2, 3], fileSystem.WrittenFiles[extractedPath]);
        Assert.DoesNotContain(content.EntryPaths.Keys, path =>
            path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_WhenCompleted_ClosesPackageStream()
    {
        byte[] archiveBytes = CreateArchive(("manifest.json", "{}"u8.ToArray()));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        var reader = new ModPackageReader(fileSystem);

        _ = reader.Read("mod.zip", "extracted", TestContext.Current.CancellationToken);

        Assert.True(fileSystem.LastOpenedStream!.IsDisposed);
    }

    [Fact]
    public void Read_WhenFileSystemFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var reader = new ModPackageReader(new StubFileSystemOperations(expected));

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() =>
            reader.Read("missing.zip", "extracted", TestContext.Current.CancellationToken));

        Assert.Same(expected, exception);
    }

    private static InvalidDataException ReadFailure(byte[] archiveBytes)
    {
        var reader = new ModPackageReader(new StubFileSystemOperations(archiveBytes));

        return Assert.Throws<InvalidDataException>(() => reader.Read("mod.zip", "extracted"));
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
        public TrackingStream? LastOpenedStream { get; private set; }

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

            LastOpenedStream = new TrackingStream(_archiveBytes!);

            return LastOpenedStream;
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

    private sealed class TrackingStream : MemoryStream
    {
        public bool IsDisposed { get; private set; }

        public TrackingStream(byte[] buffer)
            : base(buffer, writable: false) { }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
