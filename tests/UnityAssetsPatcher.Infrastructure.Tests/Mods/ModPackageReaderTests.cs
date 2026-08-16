using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Mods;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Mods;

public sealed class ModPackageReaderTests
{
    private static readonly byte[] ValidManifest =
        """
            {
              "$schema": "https://uap.cnbarrier.com/schema-v1.json",
              "name": "Test Mod",
              "author": "Test Author",
              "version": "1.0.0",
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [ { "type": "Camera", "match": { "m_Name": "Main" } } ]
                }
              ]
            }
            """u8.ToArray();

    [Fact]
    public void Open_WhenArchiveIsInvalid_ReturnsFailure()
    {
        OperationError error = OpenFailure("not a zip archive"u8.ToArray());

        Assert.Equal(ModPackageErrorCodes.InvalidArchive, error.Code);
    }

    [Fact]
    public void Open_WhenManifestIsMissing_ReturnsFailure()
    {
        byte[] archiveBytes = CreateArchive(("payload.bin", [1]));

        OperationError error = OpenFailure(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.MissingManifest, error.Code);
    }

    [Fact]
    public void Open_WhenMultipleManifestsExist_ReturnsFailure()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Nested/MANIFEST.JSON", "{}"u8.ToArray()));

        OperationError error = OpenFailure(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.MultipleManifests, error.Code);
    }

    [Fact]
    public void Open_WhenEntriesCollideIgnoringCase_ReturnsFailure()
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
    public void Open_WhenEntryPathIsUnsafe_ReturnsFailure(string entryPath)
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            (entryPath, [1]));

        OperationError error = OpenFailure(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.UnsafeEntryPath, error.Code);
        Assert.Equal(entryPath, error.Parameters["entry_path"]);
    }

    [Fact]
    public void Open_WhenTotalUncompressedSizeExceedsLimit_ReturnsFailure()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("payload-1.bin", [1]),
            ("payload-2.bin", [2]),
            ("payload-3.bin", [3]));
        SetDeclaredEntrySize(archiveBytes, "payload-1.bin", uint.MaxValue);
        SetDeclaredEntrySize(archiveBytes, "payload-2.bin", uint.MaxValue);
        SetDeclaredEntrySize(archiveBytes, "payload-3.bin", uint.MaxValue);

        OperationError error = OpenFailure(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.PackageTooLarge, error.Code);
        Assert.Equal(10L * 1024L * 1024L * 1024L, error.Parameters["maximum_bytes"]);
    }

    [Fact]
    public void Open_WhenTotalUncompressedSizeEqualsLimit_OpensPackage()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", ValidManifest),
            ("payload-1.bin", [1]),
            ("payload-2.bin", [2]),
            ("payload-3.bin", [3]));
        SetDeclaredEntrySize(archiveBytes, "payload-1.bin", uint.MaxValue);
        SetDeclaredEntrySize(archiveBytes, "payload-2.bin", uint.MaxValue);
        SetDeclaredEntrySize(archiveBytes, "payload-3.bin", checked((uint)(2_147_483_650L - ValidManifest.Length)));

        using ModPackage package = OpenPackage(new StubFileSystemOperations(archiveBytes));

        Assert.NotNull(package);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenUnrelatedEntryPathIsUnsafe_ReturnsManifest()
    {
        byte[] manifest = "{}"u8.ToArray();
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", manifest),
            ("../payload.bin", [1]));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        ModPackageReader reader = CreateReader(fileSystem);

        var result = await reader.ReadManifestAsync(
            "mod.zip",
            TestContext.Current.CancellationToken);
        byte[] manifestBytes = Assert.IsType<OperationSucceeded<byte[]>>(result).Value;

        Assert.Equal(manifest, manifestBytes);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenMultipleManifestsExist_ReturnsFailure()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Nested/MANIFEST.JSON", "{}"u8.ToArray()));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        ModPackageReader reader = CreateReader(fileSystem);

        var result = await reader.ReadManifestAsync(
            "mod.zip",
            TestContext.Current.CancellationToken);
        var failure = Assert.IsType<OperationFailed<byte[]>>(result);

        Assert.Equal(ModPackageErrorCodes.MultipleManifests, failure.Error.Code);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenManifestExceedsLimit_ReturnsFailure()
    {
        byte[] manifest = new byte[10L * 1024L * 1024L + 1];
        byte[] archiveBytes = CreateArchive(("manifest.json", manifest));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        ModPackageReader reader = CreateReader(fileSystem);

        var result = await reader.ReadManifestAsync(
            "mod.zip",
            TestContext.Current.CancellationToken);
        var failure = Assert.IsType<OperationFailed<byte[]>>(result);

        Assert.Equal(ModPackageErrorCodes.ManifestTooLarge, failure.Error.Code);
        Assert.Equal(10L * 1024L * 1024L, failure.Error.Parameters["maximum_bytes"]);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        byte[] archiveBytes = CreateArchive(("manifest.json", "{}"u8.ToArray()));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        ModPackageReader reader = CreateReader(fileSystem);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadManifestAsync("mod.zip", cancellationSource.Token));
    }

    [Fact]
    public async Task ReadManifestAsync_WhenSuccessful_LogsDecompressionMetrics()
    {
        byte[] manifest = "{}"u8.ToArray();
        byte[] archiveBytes = CreateArchive(("manifest.json", manifest));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        var loggerFactory = new RecordingLoggerFactory();
        ModPackageReader reader = CreateReader(fileSystem, loggerFactory);

        _ = await reader.ReadManifestAsync("mod.zip", TestContext.Current.CancellationToken);

        LogRecord record = Assert.Single(loggerFactory.Records, record => record.EventId.Id == 4000);
        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.Equal("manifest.json", record.Properties["ManifestEntry"]);
        Assert.Equal(Path.GetFullPath("mod.zip"), record.Properties["PackagePath"]);
        Assert.Equal((long)manifest.Length, record.Properties["ByteCount"]);
        Assert.True(Assert.IsType<double>(record.Properties["ElapsedMilliseconds"]) >= 0);
    }

    [Fact]
    public async Task CopyEntryToNewFileAsync_WhenEntryExists_WritesThroughAtomicFileSystemBoundary()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", ValidManifest),
            ("payload.bin", [1, 2, 3]));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        var loggerFactory = new RecordingLoggerFactory();
        using ModPackage package = OpenPackage(fileSystem, loggerFactory);

        var result = await package.CopyPayloadFileAsync(
            "PAYLOAD.BIN",
            "payload.output",
            TestContext.Current.CancellationToken);
        long copiedBytes = Assert.IsType<OperationSucceeded<long>>(result).Value;

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
    public async Task CopyEntryToNewFileAsync_WhenEntryIsMissing_ReturnsFailure()
    {
        byte[] archiveBytes = CreateArchive(("manifest.json", ValidManifest));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        using ModPackage package = OpenPackage(fileSystem);

        var result = await package.CopyPayloadFileAsync(
            "missing.bin",
            "payload.output",
            TestContext.Current.CancellationToken);
        var failure = Assert.IsType<OperationFailed<long>>(result);

        Assert.Equal(ModPackageErrorCodes.MissingEntry, failure.Error.Code);
        Assert.Equal("missing.bin", failure.Error.Parameters["entry_path"]);
        Assert.Empty(fileSystem.WrittenFiles);
    }

    [Fact]
    public void Open_WhenFileSystemFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var fileSystem = new StubFileSystemOperations(expected);
        ModPackageReader reader = CreateReader(fileSystem);

        var exception = Assert.Throws<FileNotFoundException>(() => OpenPackage(reader));

        Assert.Same(expected, exception);
    }

    private static OperationError OpenFailure(byte[] archiveBytes)
    {
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        ModPackageReader reader = CreateReader(fileSystem);

        var result = OpenPackageResult(reader);

        return Assert.IsType<OperationFailed<ModPackage>>(result).Error;
    }

    private static ModPackage OpenPackage(
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory? loggerFactory = null)
    {
        ModPackageReader reader = CreateReader(fileSystemOperations, loggerFactory);

        return OpenPackage(reader);
    }

    private static ModPackage OpenPackage(ModPackageReader reader)
    {
        var result = OpenPackageResult(reader);

        return Assert.IsType<OperationSucceeded<ModPackage>>(result).Value;
    }

    private static OperationResult<ModPackage> OpenPackageResult(ModPackageReader reader)
    {
        return reader.OpenAsync(
                "mod.zip",
                [],
                new StepTimer(),
                TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    private static ModPackageReader CreateReader(
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory? loggerFactory = null)
    {
        var archiveReader = new ZipModArchiveReader(fileSystemOperations);

        return new ModPackageReader(
            archiveReader,
            fileSystemOperations,
            loggerFactory ?? NullLoggerFactory.Instance);
    }

    private static byte[] CreateArchive(params (string Path, byte[] Contents)[] entries)
    {
        using var output = new MemoryStream();

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
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
            var header = archiveBytes.AsSpan(offset);

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

            return new MemoryStream(_archiveBytes!, false);
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
