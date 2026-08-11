using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Tests.Mods;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Features.Check;

public sealed class CheckManifestHandlerTests
{
    private const string ValidManifest =
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
        """;

    [Fact]
    public async Task HandleAsync_WhenManifestIsValid_ReturnsCheckResult()
    {
        var fileSystem = new StubFileSystemOperations(_ => OpenText(ValidManifest));
        var handler = CreateHandler(fileSystem);

        OperationResult<CheckManifestResult> result = await handler.HandleAsync(
            new CheckManifestRequest("manifest.json"),
            TestContext.Current.CancellationToken);

        var success = Assert.IsType<OperationSucceeded<CheckManifestResult>>(result);
        Assert.Equal("Test Mod", success.Value.Manifest.Name);
    }

    [Fact]
    public async Task HandleAsync_WhenCalled_OpensNormalizedSourcePath()
    {
        var fileSystem = new StubFileSystemOperations(_ => OpenText(ValidManifest));
        var handler = CreateHandler(fileSystem);

        _ = await handler.HandleAsync(
            new CheckManifestRequest("manifest.json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath("manifest.json"), fileSystem.OpenedPath);
    }

    [Fact]
    public async Task HandleAsync_WhenManifestIsInvalid_ReturnsManifestFailure()
    {
        var fileSystem = new StubFileSystemOperations(_ => OpenText("{}"));
        var handler = CreateHandler(fileSystem);

        OperationResult<CheckManifestResult> result = await handler.HandleAsync(
            new CheckManifestRequest("manifest.json"),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<CheckManifestResult>>(result);
        Assert.Equal(ManifestErrorCodes.MissingProperty, failure.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenDependencyFaults_PropagatesOriginalException()
    {
        var expected = new InvalidOperationException("Test fault.");
        var fileSystem = new StubFileSystemOperations(_ => throw expected);
        var handler = CreateHandler(fileSystem);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                new CheckManifestRequest("manifest.json"),
                TestContext.Current.CancellationToken));

        Assert.Same(expected, exception);
    }

    private static MemoryStream OpenText(string text)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false);
    }

    private static CheckManifestHandler CreateHandler(IFileSystemOperations fileSystemOperations)
    {
        var archiveReader = new StubModPackageReader(_ => throw new NotSupportedException());
        var packageReader = new ModPackageReader(
            archiveReader,
            fileSystemOperations,
            NullLoggerFactory.Instance);

        return new CheckManifestHandler(new ModManifestReader(fileSystemOperations, packageReader));
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        private readonly Func<string, Stream> _openRead;

        public string? OpenedPath { get; private set; }

        public StubFileSystemOperations(Func<string, Stream> openRead)
        {
            _openRead = openRead;
        }

        public Stream OpenRead(string path)
        {
            OpenedPath = path;

            return _openRead(path);
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
