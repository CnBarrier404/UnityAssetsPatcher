using System.Text;
using System.Text.Json;
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
    public async Task OpenAsync_WhenPackageReaderRejectsPackage_PropagatesException()
    {
        var expected = new InvalidDataException("invalid package");
        var packageReader = new StubModPackageReader(_ => throw expected);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => OpenPackageAsync(packageReader));

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task OpenAsync_WhenPackageReaderFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var packageReader = new StubModPackageReader(_ => throw expected);

        FileNotFoundException exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => OpenPackageAsync(packageReader));

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task CopyPayloadFile_WhenCalled_ForwardsToPackageSession()
    {
        var session = new StubModPackageSession(Encoding.UTF8.GetBytes(ValidManifest), 3);
        var packageReader = new StubModPackageReader(session);
        OperationResult<ModPackage> result = await OpenPackageAsync(packageReader);
        using ModPackage package = Assert.IsType<OperationSucceeded<ModPackage>>(result).Value;

        long copiedBytes = package.CopyPayloadFile("payload.bin", "payload.output");

        Assert.Equal(3, copiedBytes);
        Assert.Equal("payload.bin", session.CopiedSource);
        Assert.Equal("payload.output", session.CopyDestinationPath);
    }

    [Fact]
    public async Task ReadAsync_WhenPackageManifestJsonIsInvalid_PropagatesJsonException()
    {
        var session = new StubModPackageSession(Encoding.UTF8.GetBytes("{"));
        var packageReader = new StubModPackageReader(session);
        var reader = new ModManifestReader(new StubFileSystemOperations(), packageReader);

        _ = await Assert.ThrowsAnyAsync<JsonException>(() =>
            reader.ReadAsync("mod.zip", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_WhenSourceIsPackage_OpensNormalizedPath()
    {
        var session = new StubModPackageSession(Encoding.UTF8.GetBytes(ValidManifest));
        var packageReader = new StubModPackageReader(session);
        var reader = new ModManifestReader(new StubFileSystemOperations(), packageReader);

        _ = await reader.ReadAsync("mod.zip", TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath("mod.zip"), packageReader.OpenedPath);
    }

    private static Task<OperationResult<ModPackage>> OpenPackageAsync(IModPackageReader modPackageReader)
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
            throw new NotSupportedException();
        }

        public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode)
        {
            throw new NotSupportedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotSupportedException();
        }

        public void EnsureDirectory(string path)
        {
            throw new NotSupportedException();
        }

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
