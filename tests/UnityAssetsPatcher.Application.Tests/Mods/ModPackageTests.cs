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
    public void Open_WhenPackageReaderReturnsFailure_PropagatesFailure()
    {
        var expected = new OperationError(ModPackageErrorCodes.ManifestMissing);
        var packageReader = new StubPackageReader(_ => new OperationFailed<IPackageSession>(expected));

        OperationResult<ModPackage> result = OpenPackage(packageReader);

        var failure = Assert.IsType<OperationFailed<ModPackage>>(result);
        Assert.Same(expected, failure.Error);
    }

    [Fact]
    public void Open_WhenPackageReaderFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var packageReader = new StubPackageReader(_ => throw expected);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => OpenPackage(packageReader));

        Assert.Same(expected, exception);
    }

    [Fact]
    public void CopyPayloadFile_WhenCalled_ForwardsToPackageSession()
    {
        var copyResult = new OperationSucceeded<long>(3);
        var session = new StubPackageSession(Encoding.UTF8.GetBytes(ValidManifest), copyResult);
        var packageReader = new StubPackageReader(session);
        using ModPackage package = Assert.IsType<OperationSucceeded<ModPackage>>(OpenPackage(packageReader)).Value;

        OperationResult<long> result = package.CopyPayloadFile("payload.bin", "payload.output");

        Assert.Same(copyResult, result);
        Assert.Equal("payload.bin", session.CopiedSource);
        Assert.Equal("payload.output", session.CopyDestinationPath);
    }

    [Fact]
    public async Task ReadAsync_WhenPackageManifestJsonIsInvalid_PropagatesJsonException()
    {
        var session = new StubPackageSession(Encoding.UTF8.GetBytes("{"));
        var packageReader = new StubPackageReader(session);
        var reader = new ModManifestReader(new StubFileSystemOperations(), packageReader);

        _ = await Assert.ThrowsAnyAsync<JsonException>(() =>
            reader.ReadAsync("mod.zip", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_WhenSourceIsPackage_OpensNormalizedPath()
    {
        var session = new StubPackageSession(Encoding.UTF8.GetBytes(ValidManifest));
        var packageReader = new StubPackageReader(session);
        var reader = new ModManifestReader(new StubFileSystemOperations(), packageReader);

        _ = await reader.ReadAsync("mod.zip", TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath("mod.zip"), packageReader.OpenedPath);
    }

    private static OperationResult<ModPackage> OpenPackage(IPackageReader packageReader)
    {
        return ModPackage.Open(
            "mod.zip",
            [],
            packageReader,
            new StubFileSystemOperations(),
            new StepTimer());
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
