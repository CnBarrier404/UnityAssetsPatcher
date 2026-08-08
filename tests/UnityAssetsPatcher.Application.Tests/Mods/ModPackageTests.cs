using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Mods;

public sealed class ModPackageTests
{
    private const string ValidManifest =
        """
        {
          "$schema": "https://uap.cnbarrier.com/schema-v1.json",
          "name": "Test Mod",
          "author": "Test Author",
          "version": "1.0.0",
          "copyFiles": [ { "source": "payload.bin" } ],
          "targets": [
            {
              "file": "sharedassets0.assets",
              "patches": [ { "type": "Camera", "match": { "m_Name": "Main" } } ]
            }
          ]
        }
        """;

    [Fact]
    public async Task Check_WhenManifestIsMissing_ReturnsStructuredFailure()
    {
        byte[] archiveBytes = CreateArchive(("payload.bin", [1]));

        OperationError error = await CheckFailureAsync(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.ManifestMissing, error.Code);
    }

    [Fact]
    public async Task Check_WhenMultipleManifestsExist_ReturnsStructuredFailure()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Nested/MANIFEST.JSON", "{}"u8.ToArray()));

        OperationError error = await CheckFailureAsync(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.MultipleManifests, error.Code);
    }

    [Fact]
    public async Task Check_WhenEntriesCollideIgnoringCase_ReturnsStructuredFailure()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("Payload/file.bin", [1]),
            ("payload/FILE.BIN", [2]));

        OperationError error = await CheckFailureAsync(archiveBytes);

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
    public async Task Check_WhenEntryPathIsUnsafe_ReturnsStructuredFailure(string entryPath)
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", "{}"u8.ToArray()),
            (entryPath, [1]));

        OperationError error = await CheckFailureAsync(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.UnsafeEntryPath, error.Code);
        Assert.Equal(entryPath, error.Parameters["entry_path"]);
    }

    [Fact]
    public async Task Check_WhenManifestExceedsLimit_ReturnsStructuredFailure()
    {
        byte[] manifest = new byte[10L * 1024L * 1024L + 1];
        byte[] archiveBytes = CreateArchive(("manifest.json", manifest));

        OperationError error = await CheckFailureAsync(archiveBytes);

        Assert.Equal(ModPackageErrorCodes.ManifestTooLarge, error.Code);
        Assert.Equal(10L * 1024L * 1024L, error.Parameters["limit_bytes"]);
    }

    [Fact]
    public void CopyPayloadFile_WhenEntryExists_WritesThroughAtomicFileSystemBoundary()
    {
        byte[] archiveBytes = CreateArchive(
            ("manifest.json", Encoding.UTF8.GetBytes(ValidManifest)),
            ("payload.bin", [1, 2, 3]));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        using ModPackage package = OpenPackage(fileSystem);

        OperationResult<long> result = package.CopyPayloadFile("PAYLOAD.BIN", "payload.output");

        var success = Assert.IsType<OperationSucceeded<long>>(result);
        Assert.Equal(3, success.Value);
        Assert.Equal([1, 2, 3], Assert.Single(fileSystem.WrittenFiles).Value);
    }

    [Fact]
    public void CopyPayloadFile_WhenEntryIsMissing_ReturnsStructuredFailure()
    {
        byte[] archiveBytes = CreateArchive(("manifest.json", Encoding.UTF8.GetBytes(ValidManifest)));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        using ModPackage package = OpenPackage(fileSystem);

        OperationResult<long> result = package.CopyPayloadFile("missing.bin", "payload.output");

        var failure = Assert.IsType<OperationFailed<long>>(result);
        Assert.Equal(ModPackageErrorCodes.EntryNotFound, failure.Error.Code);
        Assert.Empty(fileSystem.WrittenFiles);
    }

    [Fact]
    public void Open_WhenFileSystemFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var fileSystem = new StubFileSystemOperations(expected);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => OpenPackage(fileSystem));

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task ReadAsync_WhenManifestJsonIsInvalid_PropagatesJsonException()
    {
        byte[] archiveBytes = CreateArchive(("manifest.json", Encoding.UTF8.GetBytes("{")));
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        var reader = new ModManifestReader(fileSystem);

        _ = await Assert.ThrowsAnyAsync<JsonException>(() =>
            reader.ReadAsync("mod.zip", TestContext.Current.CancellationToken));
    }

    private static ModPackage OpenPackage(IFileSystemOperations fileSystemOperations)
    {
        OperationResult<ModPackage> result = ModPackage.Open(
            "mod.zip",
            [],
            fileSystemOperations,
            new StepTimer());
        var success = Assert.IsType<OperationSucceeded<ModPackage>>(result);

        return success.Value;
    }

    private static async Task<OperationError> CheckFailureAsync(byte[] archiveBytes)
    {
        var fileSystem = new StubFileSystemOperations(archiveBytes);
        var handler = new CheckManifestHandler(new ModManifestReader(fileSystem));

        OperationResult<CheckManifestResult> result = await handler.HandleAsync(
            new CheckManifestRequest("mod.zip"),
            TestContext.Current.CancellationToken);
        var failure = Assert.IsType<OperationFailed<CheckManifestResult>>(result);

        return failure.Error;
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
