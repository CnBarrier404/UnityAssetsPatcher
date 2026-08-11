using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task OpenAsync_WhenPackageReaderRejectsPackage_ReturnsFailure()
    {
        var archiveReader = new StubModPackageReader(_ => throw new InvalidDataException());
        var packageReader = CreatePackageReader(archiveReader, new StubFileSystemOperations());

        OperationResult<ModPackage> result = await OpenPackageAsync(packageReader);
        var failure = Assert.IsType<OperationFailed<ModPackage>>(result);

        Assert.Equal(ModPackageErrorCodes.InvalidArchive, failure.Error.Code);
    }

    [Fact]
    public async Task OpenAsync_WhenPackageReaderFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var archiveReader = new StubModPackageReader(_ => throw expected);
        var packageReader = CreatePackageReader(archiveReader, new StubFileSystemOperations());

        FileNotFoundException exception =
            await Assert.ThrowsAsync<FileNotFoundException>(() => OpenPackageAsync(packageReader));

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task CopyPayloadFile_WhenCalled_ForwardsToPackageSession()
    {
        var session = new StubModPackageSession(
            Encoding.UTF8.GetBytes(ValidManifest),
            ("payload.bin", new byte[] { 1, 2, 3 }));
        var archiveReader = new StubModPackageReader(session);
        var fileSystemOperations = new StubFileSystemOperations();
        ModPackageReader packageReader = CreatePackageReader(archiveReader, fileSystemOperations);
        OperationResult<ModPackage> result = await OpenPackageAsync(packageReader);
        using ModPackage package = Assert.IsType<OperationSucceeded<ModPackage>>(result).Value;

        OperationResult<long> copyResult = await package.CopyPayloadFileAsync(
            "payload.bin",
            "payload.output",
            TestContext.Current.CancellationToken);
        long copiedBytes = Assert.IsType<OperationSucceeded<long>>(copyResult).Value;

        Assert.Equal(3, copiedBytes);
        Assert.Equal([1, 2, 3], fileSystemOperations.WrittenBytes);
    }

    [Fact]
    public async Task ReadAsync_WhenPackageManifestJsonIsInvalid_ReturnsFailure()
    {
        var session = new StubModPackageSession(Encoding.UTF8.GetBytes("{"));
        var archiveReader = new StubModPackageReader(session);
        var fileSystemOperations = new StubFileSystemOperations();
        ModPackageReader packageReader = CreatePackageReader(archiveReader, fileSystemOperations);
        var reader = new ModManifestReader(fileSystemOperations, packageReader);

        OperationResult<ModManifest> result = await reader.ReadAsync(
            "mod.zip",
            TestContext.Current.CancellationToken);
        var failure = Assert.IsType<OperationFailed<ModManifest>>(result);

        Assert.Equal(ManifestErrorCodes.InvalidJson, failure.Error.Code);
    }

    [Fact]
    public async Task ReadAsync_WhenSourceIsPackage_ReadsManifestFromNormalizedPath()
    {
        var session = new StubModPackageSession(Encoding.UTF8.GetBytes(ValidManifest));
        var archiveReader = new StubModPackageReader(session);
        var fileSystemOperations = new StubFileSystemOperations();
        ModPackageReader packageReader = CreatePackageReader(archiveReader, fileSystemOperations);
        var reader = new ModManifestReader(fileSystemOperations, packageReader);

        _ = await reader.ReadAsync("mod.zip", TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath("mod.zip"), archiveReader.OpenedPath);
    }

    [Fact]
    public async Task ReadAsync_WhenSourceOnlyStartsWithPk_DoesNotProbeArchive()
    {
        var archiveReader = new StubModPackageReader(_ => throw new InvalidOperationException());
        var fileSystemOperations = new StubFileSystemOperations("PK invalid json"u8.ToArray());
        ModPackageReader packageReader = CreatePackageReader(archiveReader, fileSystemOperations);
        var reader = new ModManifestReader(fileSystemOperations, packageReader);

        OperationResult<ModManifest> result = await reader.ReadAsync(
            "manifest.json",
            TestContext.Current.CancellationToken);
        var failure = Assert.IsType<OperationFailed<ModManifest>>(result);

        Assert.Equal(ManifestErrorCodes.InvalidJson, failure.Error.Code);
        Assert.Null(archiveReader.OpenedPath);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenDecompressedSizeDiffersFromDeclaredSize_ReturnsFailure()
    {
        byte[] manifestBytes = Encoding.UTF8.GetBytes(ValidManifest);
        var session = new StubModPackageSession(manifestBytes, manifestBytes.Length + 1L);
        var archiveReader = new StubModPackageReader(session);
        var fileSystemOperations = new StubFileSystemOperations();
        ModPackageReader packageReader = CreatePackageReader(archiveReader, fileSystemOperations);

        OperationResult<byte[]> result = await packageReader.ReadManifestAsync(
            "mod.zip",
            TestContext.Current.CancellationToken);
        var failure = Assert.IsType<OperationFailed<byte[]>>(result);

        Assert.Equal(ModPackageErrorCodes.EntrySizeMismatch, failure.Error.Code);
        Assert.Equal((long)manifestBytes.Length + 1, failure.Error.Parameters["declared_bytes"]);
        Assert.Equal((long)manifestBytes.Length, failure.Error.Parameters["observed_bytes"]);
    }

    private static ModPackageReader CreatePackageReader(
        IModPackageReader packageReader,
        IFileSystemOperations fileSystemOperations)
    {
        return new ModPackageReader(packageReader, fileSystemOperations, NullLoggerFactory.Instance);
    }

    private static Task<OperationResult<ModPackage>> OpenPackageAsync(ModPackageReader modPackageReader)
    {
        return ModPackage.OpenAsync(
            "mod.zip",
            [],
            modPackageReader,
            new StubFileSystemOperations(),
            new StepTimer(),
            TestContext.Current.CancellationToken);
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        private readonly byte[] _sourceBytes;

        public byte[]? WrittenBytes { get; private set; }

        public StubFileSystemOperations()
            : this([(byte)'P', (byte)'K', 0x03, 0x04]) { }

        public StubFileSystemOperations(byte[] sourceBytes)
        {
            _sourceBytes = sourceBytes;
        }

        public Stream OpenRead(string path)
        {
            return new MemoryStream(_sourceBytes, writable: false);
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

            WrittenBytes = output.ToArray();
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

        public void DeleteDirectoryTree(string path) { }
    }
}
