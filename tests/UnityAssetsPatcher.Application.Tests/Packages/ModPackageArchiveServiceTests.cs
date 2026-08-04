using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Packages;

public sealed class ModPackageArchiveServiceTests
{
    private const long SixGiB = 6L * 1024L * 1024L * 1024L;

    [Fact]
    public void OpenRead_WhenManifestIsMissing_ReturnsStructuredFailureAndDisposesArchive()
    {
        var archive = new StubArchive([new StubEntry("payload.bin", [1])]);
        ModPackageArchiveService service = CreateService(archive);

        OperationResult<ModPackageArchiveSession> result = service.OpenRead(archive.PackagePath);

        var failure = Assert.IsType<OperationFailed<ModPackageArchiveSession>>(result);
        Assert.Equal(ModPackageErrorCodes.ManifestMissing, failure.Error.Code);
        Assert.True(archive.IsDisposed);
    }

    [Fact]
    public void OpenRead_WhenMultipleManifestsExist_ReturnsStructuredFailure()
    {
        var archive = new StubArchive(
        [
            new StubEntry("manifest.json", "{}"u8.ToArray()),
            new StubEntry("Nested/MANIFEST.JSON", "{}"u8.ToArray()),
        ]);
        ModPackageArchiveService service = CreateService(archive);

        OperationError error = OpenFailure(service, archive.PackagePath);

        Assert.Equal(ModPackageErrorCodes.MultipleManifests, error.Code);
    }

    [Fact]
    public void OpenRead_WhenEntriesCollideIgnoringCase_ReturnsStructuredFailure()
    {
        var archive = new StubArchive(
        [
            new StubEntry("manifest.json", "{}"u8.ToArray()),
            new StubEntry("Payload/file.bin", [1]),
            new StubEntry("payload/FILE.BIN", [2]),
        ]);
        ModPackageArchiveService service = CreateService(archive);

        OperationError error = OpenFailure(service, archive.PackagePath);

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
    public void OpenRead_WhenEntryPathIsUnsafe_ReturnsStructuredFailure(string entryPath)
    {
        var archive = new StubArchive(
        [
            new StubEntry("manifest.json", "{}"u8.ToArray()),
            new StubEntry(entryPath, [1]),
        ]);
        ModPackageArchiveService service = CreateService(archive);

        OperationError error = OpenFailure(service, archive.PackagePath);

        Assert.Equal(ModPackageErrorCodes.UnsafeEntryPath, error.Code);
        Assert.Equal(entryPath, error.Parameters["entry_path"]);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenDeclaredLengthExceedsLimit_ReturnsStructuredFailure()
    {
        var manifest = new StubEntry("manifest.json", [], 10L * 1024L * 1024L + 1);
        var archive = new StubArchive([manifest]);
        ModPackageArchiveService service = CreateService(archive);

        using ModPackageArchiveSession session = Open(service, archive.PackagePath);
        OperationResult<byte[]> result = await session.ReadManifestAsync(TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<byte[]>>(result);
        Assert.Equal(ModPackageErrorCodes.ManifestTooLarge, failure.Error.Code);
        Assert.Equal(10L * 1024L * 1024L, failure.Error.Parameters["limit_bytes"]);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenCumulativeExtractionExceedsLimit_ReturnsStructuredFailure()
    {
        var archive = new StubArchive(
        [
            new StubEntry("manifest.json", "{}"u8.ToArray()),
            new StubEntry("first.bin", [], SixGiB),
            new StubEntry("second.bin", [], SixGiB),
        ]);
        var fileSystem = new StubFileSystemOperations();
        ModPackageArchiveService service = CreateService(archive, fileSystem);

        using ModPackageArchiveSession session = Open(service, archive.PackagePath);
        OperationResult<long> firstResult = session.CopyEntryToNewFile(
            "first.bin",
            "first.output",
            TestContext.Current.CancellationToken);

        OperationResult<long> secondResult = session.CopyEntryToNewFile(
            "second.bin",
            "second.output",
            TestContext.Current.CancellationToken);

        Assert.IsType<OperationSucceeded<long>>(firstResult);
        var failure = Assert.IsType<OperationFailed<long>>(secondResult);
        Assert.Equal(ModPackageErrorCodes.ExtractionLimitExceeded, failure.Error.Code);
        Assert.Equal("second.bin", failure.Error.Parameters["entry_path"]);
        Assert.Single(fileSystem.WrittenPaths);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenEntryExists_WritesThroughAtomicFileSystemBoundary()
    {
        var archive = new StubArchive(
        [
            new StubEntry("manifest.json", "{}"u8.ToArray()),
            new StubEntry("Payload/File.bin", [1, 2, 3]),
        ]);
        var fileSystem = new StubFileSystemOperations();
        ModPackageArchiveService service = CreateService(archive, fileSystem);

        using ModPackageArchiveSession session = Open(service, archive.PackagePath);
        OperationResult<long> result = session.CopyEntryToNewFile(
            "payload/file.BIN",
            "payload.output",
            TestContext.Current.CancellationToken);

        var success = Assert.IsType<OperationSucceeded<long>>(result);
        Assert.Equal(3, success.Value);
        Assert.Equal([1, 2, 3], Assert.Single(fileSystem.WrittenFiles).Value);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenEntryIsMissing_ReturnsStructuredFailure()
    {
        var archive = new StubArchive([new StubEntry("manifest.json", "{}"u8.ToArray())]);
        var fileSystem = new StubFileSystemOperations();
        ModPackageArchiveService service = CreateService(archive, fileSystem);

        using ModPackageArchiveSession session = Open(service, archive.PackagePath);
        OperationResult<long> result = session.CopyEntryToNewFile(
            "missing.bin",
            "missing.output",
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<long>>(result);
        Assert.Equal(ModPackageErrorCodes.EntryNotFound, failure.Error.Code);
        Assert.Empty(fileSystem.WrittenPaths);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenCancellationIsAlreadyRequested_DoesNotWriteFile()
    {
        var archive = new StubArchive(
        [
            new StubEntry("manifest.json", "{}"u8.ToArray()),
            new StubEntry("payload.bin", [1, 2, 3]),
        ]);
        var fileSystem = new StubFileSystemOperations();
        ModPackageArchiveService service = CreateService(archive, fileSystem);
        using CancellationTokenSource cancellation = new();

        cancellation.Cancel();

        using ModPackageArchiveSession session = Open(service, archive.PackagePath);

        _ = Assert.ThrowsAny<OperationCanceledException>(() =>
            session.CopyEntryToNewFile("payload.bin", "payload.output", cancellation.Token));

        Assert.Empty(fileSystem.WrittenPaths);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenFileSystemFails_PropagatesStandardException()
    {
        var expected = new IOException("write failed");
        var archive = new StubArchive(
        [
            new StubEntry("manifest.json", "{}"u8.ToArray()),
            new StubEntry("payload.bin", [1, 2, 3]),
        ]);
        var fileSystem = new StubFileSystemOperations(expected);
        ModPackageArchiveService service = CreateService(archive, fileSystem);

        using ModPackageArchiveSession session = Open(service, archive.PackagePath);

        IOException exception = Assert.Throws<IOException>(() => session.CopyEntryToNewFile(
            "payload.bin",
            "payload.output",
            TestContext.Current.CancellationToken));

        Assert.Same(expected, exception);
    }

    [Fact]
    public void OpenRead_WhenInfrastructureFails_PropagatesStandardException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var archiveFactory = new StubArchiveFactory(_ => throw expected);
        var service = new ModPackageArchiveService(archiveFactory, new StubFileSystemOperations());

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => service.OpenRead("missing.zip"));

        Assert.Same(expected, exception);
    }

    private static ModPackageArchiveService CreateService(
        IModPackageArchive archive,
        IFileSystemOperations? fileSystemOperations = null)
    {
        return new ModPackageArchiveService(
            new StubArchiveFactory(_ => archive),
            fileSystemOperations ?? new StubFileSystemOperations());
    }

    private static ModPackageArchiveSession Open(ModPackageArchiveService service, string packagePath)
    {
        OperationResult<ModPackageArchiveSession> result = service.OpenRead(packagePath);
        var success = Assert.IsType<OperationSucceeded<ModPackageArchiveSession>>(result);

        return success.Value;
    }

    private static OperationError OpenFailure(ModPackageArchiveService service, string packagePath)
    {
        OperationResult<ModPackageArchiveSession> result = service.OpenRead(packagePath);
        var failure = Assert.IsType<OperationFailed<ModPackageArchiveSession>>(result);

        return failure.Error;
    }

    private sealed class StubArchiveFactory : IModPackageArchiveFactory
    {
        private readonly Func<string, IModPackageArchive> _openRead;

        public StubArchiveFactory(Func<string, IModPackageArchive> openRead)
        {
            _openRead = openRead;
        }

        public IModPackageArchive OpenRead(string packagePath)
        {
            return _openRead(packagePath);
        }
    }

    private sealed class StubArchive : IModPackageArchive
    {
        public string PackagePath { get; } = Path.GetFullPath("test-package.zip");

        public IReadOnlyList<PackageEntryInfo> Entries { get; }

        public bool IsDisposed { get; private set; }

        private readonly IReadOnlyList<StubEntry> _entries;

        public StubArchive(IReadOnlyList<StubEntry> entries)
        {
            _entries = entries;
            Entries = Array.AsReadOnly(
            [
                .. entries.Select((entry, index) => new PackageEntryInfo(
                    new PackageEntryId(index),
                    entry.Path,
                    entry.Length,
                    entry.IsDirectory)),
            ]);
        }

        public Stream OpenEntry(PackageEntryId entryId)
        {
            return _entries[entryId.Value].OpenRead();
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class StubEntry
    {
        public string Path { get; }

        public long Length { get; }

        public bool IsDirectory { get; }

        private readonly byte[] _contents;

        public StubEntry(string fullName, byte[] contents, long? declaredLength = null, bool isDirectory = false)
        {
            Path = fullName;
            _contents = contents;
            Length = declaredLength ?? contents.LongLength;
            IsDirectory = isDirectory;
        }

        public Stream OpenRead()
        {
            return new MemoryStream(_contents, writable: false);
        }
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        public List<string> WrittenPaths { get; } = [];

        public Dictionary<string, byte[]> WrittenFiles { get; } = new(StringComparer.Ordinal);

        private readonly Exception? _writeException;

        public StubFileSystemOperations(Exception? writeException = null)
        {
            _writeException = writeException;
        }

        public Stream OpenRead(string path)
        {
            throw new NotSupportedException();
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
            if (_writeException is not null)
            {
                throw _writeException;
            }

            using MemoryStream output = new();

            writer(output);

            WrittenPaths.Add(destinationPath);
            WrittenFiles.Add(destinationPath, output.ToArray());
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
