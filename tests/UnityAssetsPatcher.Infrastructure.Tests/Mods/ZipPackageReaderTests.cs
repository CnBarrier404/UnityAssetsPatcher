using System.IO.Compression;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Mods;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Mods;

public sealed class ZipPackageReaderTests
{
    [Fact]
    public void Open_WhenManifestIsMissing_ReturnsStructuredFailure()
    {
        byte[] archiveBytes = CreateArchive(("payload.bin", [1]));

        OperationError error = OpenFailure(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.ManifestMissing, error.Code);
    }

    [Fact]
    public void Open_WhenMultipleManifestsExist_ReturnsStructuredFailure()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Nested/MANIFEST.JSON", "{}"u8.ToArray()));

        OperationError error = OpenFailure(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.MultipleManifests, error.Code);
    }

    [Fact]
    public void Open_WhenEntriesCollideIgnoringCase_ReturnsStructuredFailure()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Payload/file.bin", [1]),
            ("payload/FILE.BIN", [2]));

        OperationError error = OpenFailure(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.DuplicateEntry, error.Code);
        Assert.Equal("payload/FILE.BIN", error.Parameters["entry_path"]);
    }

    [Theory]
    [InlineData("../payload.bin")]
    [InlineData("/payload.bin")]
    [InlineData("payload/./file.bin")]
    [InlineData("payload//file.bin")]
    [InlineData("C:/payload.bin")]
    [InlineData("payload/file.bin.")]
    public void Open_WhenEntryPathIsUnsafe_ReturnsStructuredFailure(string entryPath)
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            (entryPath, [1]));

        OperationError error = OpenFailure(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.UnsafeEntryPath, error.Code);
        Assert.Equal(entryPath, error.Parameters["entry_path"]);
    }

    [Fact]
    public void ReadManifest_WhenManifestExceedsLimit_ReturnsStructuredFailure()
    {
        byte[] manifest = new byte[10L * 1024L * 1024L + 1];
        byte[] archiveBytes = CreateArchive(("manifest.json", manifest));
        using IPackageSession session = OpenPackage(new StubFileSystemOperations(archiveBytes));

        OperationResult<byte[]> result = session.ReadManifest();

        var failure = Assert.IsType<OperationFailed<byte[]>>(result);
        Assert.Equal(ModPackageErrorCodes.ManifestTooLarge, failure.Error.Code);
        Assert.Equal(10L * 1024L * 1024L, failure.Error.Parameters["limit_bytes"]);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenEntryExists_WritesThroughAtomicFileSystemBoundary()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("payload.bin", [1, 2, 3]));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        using IPackageSession session = OpenPackage(fileSystem);

        OperationResult<long> result = session.CopyEntryToNewFile(
            "PAYLOAD.BIN",
            "payload.output",
            TestContext.Current.CancellationToken);

        var success = Assert.IsType<OperationSucceeded<long>>(result);
        Assert.Equal(3, success.Value);
        Assert.Equal([1, 2, 3], Assert.Single(fileSystem.WrittenFiles).Value);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenEntryIsMissing_ReturnsStructuredFailure()
    {
        byte[] archiveBytes = CreateArchive(("manifest.json", "{}"u8.ToArray()));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        using IPackageSession session = OpenPackage(fileSystem);

        OperationResult<long> result = session.CopyEntryToNewFile(
            "missing.bin",
            "payload.output",
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<long>>(result);
        Assert.Equal(ModPackageErrorCodes.EntryNotFound, failure.Error.Code);
        Assert.Empty(fileSystem.WrittenFiles);
    }

    [Fact]
    public void Open_WhenFileSystemFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var reader = new ZipPackageReader(new StubFileSystemOperations(expected));

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => reader.Open("missing.zip"));

        Assert.Same(expected, exception);
    }

    private static OperationError OpenFailure(byte[] archiveBytes)
    {
        var reader = new ZipPackageReader(new StubFileSystemOperations(archiveBytes));

        OperationResult<IPackageSession> result = reader.Open("mod.zip");
        var failure = Assert.IsType<OperationFailed<IPackageSession>>(result);

        return failure.Error;
    }

    private static IPackageSession OpenPackage(IFileSystemOperations fileSystemOperations)
    {
        var reader = new ZipPackageReader(fileSystemOperations);

        OperationResult<IPackageSession> result = reader.Open("mod.zip");
        var success = Assert.IsType<OperationSucceeded<IPackageSession>>(result);

        return success.Value;
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
