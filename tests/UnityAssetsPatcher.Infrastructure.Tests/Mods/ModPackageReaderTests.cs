using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    public void Open_WhenTotalUncompressedSizeExceedsLimit_ThrowsInvalidDataException()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("payload-1.bin", [1]),
            ("payload-2.bin", [2]),
            ("payload-3.bin", [3]));
        SetDeclaredEntrySize(archiveBytes, "payload-1.bin", uint.MaxValue);
        SetDeclaredEntrySize(archiveBytes, "payload-2.bin", uint.MaxValue);
        SetDeclaredEntrySize(archiveBytes, "payload-3.bin", uint.MaxValue);

        InvalidDataException exception = OpenFailure(archiveBytes);

        Assert.Contains("10737418240-byte uncompressed size limit", exception.Message);
    }

    [Fact]
    public void Open_WhenTotalUncompressedSizeEqualsLimit_OpensPackage()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("payload-1.bin", [1]),
            ("payload-2.bin", [2]),
            ("payload-3.bin", [3]));
        SetDeclaredEntrySize(archiveBytes, "payload-1.bin", uint.MaxValue);
        SetDeclaredEntrySize(archiveBytes, "payload-2.bin", uint.MaxValue);
        SetDeclaredEntrySize(archiveBytes, "payload-3.bin", 2_147_483_648);

        using IModPackageSession session = OpenPackage(new StubFileSystemOperations(archiveBytes));

        Assert.NotNull(session);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenManifestExceedsLimit_ThrowsInvalidDataException()
    {
        byte[] manifest = new byte[10L * 1024L * 1024L + 1];
        byte[] archiveBytes = CreateArchive(("manifest.json", manifest));
        using IModPackageSession session = OpenPackage(new StubFileSystemOperations(archiveBytes));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => session.ReadManifestAsync(TestContext.Current.CancellationToken));

        Assert.Contains("10485760-byte limit", exception.Message);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        byte[] archiveBytes = CreateArchive(("manifest.json", "{}"u8.ToArray()));
        using IModPackageSession session = OpenPackage(new StubFileSystemOperations(archiveBytes));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.ReadManifestAsync(cancellationSource.Token));
    }

    [Fact]
    public async Task ReadManifestAsync_WhenSuccessful_LogsDecompressionMetrics()
    {
        byte[] manifest = "{}"u8.ToArray();
        byte[] archiveBytes = CreateArchive(("manifest.json", manifest));
        var loggerFactory = new RecordingLoggerFactory();
        using IModPackageSession session = OpenPackage(
            new StubFileSystemOperations(archiveBytes),
            loggerFactory);

        _ = await session.ReadManifestAsync(TestContext.Current.CancellationToken);

        LogRecord record = Assert.Single(loggerFactory.Records, record => record.EventId.Id == 4000);
        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.Equal("manifest.json", record.Properties["ManifestEntry"]);
        Assert.Equal(Path.GetFullPath("mod.zip"), record.Properties["PackagePath"]);
        Assert.Equal((long)manifest.Length, record.Properties["ByteCount"]);
        Assert.True(Assert.IsType<double>(record.Properties["ElapsedMilliseconds"]) >= 0);
    }

    [Fact]
    public void CopyEntryToNewFile_WhenEntryExists_WritesThroughAtomicFileSystemBoundary()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("payload.bin", [1, 2, 3]));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        var loggerFactory = new RecordingLoggerFactory();
        using IModPackageSession session = OpenPackage(fileSystem, loggerFactory);

        long copiedBytes = session.CopyEntryToNewFile(
            "PAYLOAD.BIN",
            "payload.output",
            TestContext.Current.CancellationToken);

        Assert.Equal(3, copiedBytes);
        Assert.Equal([1, 2, 3], Assert.Single(fileSystem.WrittenFiles).Value);
        LogRecord record = Assert.Single(loggerFactory.Records, record => record.EventId.Id == 4001);
        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.Equal("payload.bin", record.Properties["EntryPath"]);
        Assert.Equal(Path.GetFullPath("mod.zip"), record.Properties["PackagePath"]);
        Assert.Equal(Path.GetFullPath("payload.output"), record.Properties["DestinationPath"]);
        Assert.Equal(3L, record.Properties["ByteCount"]);
        Assert.True(Assert.IsType<double>(record.Properties["ElapsedMilliseconds"]) >= 0);
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
        var reader = new ModPackageReader(
            new StubFileSystemOperations(expected),
            NullLoggerFactory.Instance);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => reader.Open("missing.zip"));

        Assert.Same(expected, exception);
    }

    private static InvalidDataException OpenFailure(byte[] archiveBytes)
    {
        var reader = new ModPackageReader(
            new StubFileSystemOperations(archiveBytes),
            NullLoggerFactory.Instance);

        return Assert.Throws<InvalidDataException>(() => reader.Open("mod.zip"));
    }

    private static IModPackageSession OpenPackage(
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory? loggerFactory = null)
    {
        var reader = new ModPackageReader(fileSystemOperations, loggerFactory ?? NullLoggerFactory.Instance);

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

    private static void SetDeclaredEntrySize(byte[] archiveBytes, string entryPath, uint size)
    {
        ReadOnlySpan<byte> centralDirectorySignature = [0x50, 0x4b, 0x01, 0x02];

        for (int offset = 0; offset <= archiveBytes.Length - 46; offset++)
        {
            Span<byte> header = archiveBytes.AsSpan(offset);

            if (!header.StartsWith(centralDirectorySignature))
            {
                continue;
            }

            ushort fileNameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);
            string fileName = Encoding.UTF8.GetString(header.Slice(46, fileNameLength));

            if (string.Equals(fileName, entryPath, StringComparison.Ordinal))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(header[24..], size);

                return;
            }

            ushort extraFieldLength = BinaryPrimitives.ReadUInt16LittleEndian(header[30..]);
            ushort commentLength = BinaryPrimitives.ReadUInt16LittleEndian(header[32..]);
            offset += 45 + fileNameLength + extraFieldLength + commentLength;
        }

        throw new InvalidOperationException($"The archive entry was not found: {entryPath}");
    }

    private sealed record LogRecord(
        LogLevel Level,
        EventId EventId,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<LogRecord> Records { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(Records);
        }

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly ICollection<LogRecord> _records;

        public RecordingLogger(ICollection<LogRecord> records)
        {
            _records = records;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            IReadOnlyDictionary<string, object?> properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            _records.Add(new LogRecord(logLevel, eventId, properties));
        }
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
