using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Mods;

internal sealed class ManifestTestHost
{
    private readonly CheckManifestHandler _handler;

    private ManifestTestHost(CheckManifestHandler handler)
    {
        _handler = handler;
    }

    public static ManifestTestHost FromText(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return FromBytes(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static ManifestTestHost FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return Create(_ => new MemoryStream(bytes, writable: false));
    }

    public static ManifestTestHost Create(Func<string, Stream> openRead)
    {
        ArgumentNullException.ThrowIfNull(openRead);

        var fileSystemOperations = new StubFileSystemOperations(openRead);
        var archiveReader = new StubModArchiveReader(_ => throw new NotSupportedException());
        var packageReader = new ModPackageReader(
            archiveReader,
            fileSystemOperations,
            NullLoggerFactory.Instance);

        return new ManifestTestHost(
            new CheckManifestHandler(new ModManifestReader(fileSystemOperations, packageReader)));
    }

    public ModManifest Read(string sourcePath = "manifest.json")
    {
        OperationResult<CheckManifestResult> result = ReadResult(sourcePath);
        var success = Assert.IsType<OperationSucceeded<CheckManifestResult>>(result);

        return success.Value.Manifest;
    }

    public OperationError ReadFailure(string sourcePath = "manifest.json")
    {
        OperationResult<CheckManifestResult> result = ReadResult(sourcePath);
        var failure = Assert.IsType<OperationFailed<CheckManifestResult>>(result);

        return failure.Error;
    }

    private OperationResult<CheckManifestResult> ReadResult(string sourcePath)
    {
        return _handler
            .HandleAsync(new CheckManifestRequest(sourcePath), TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        private readonly Func<string, Stream> _openRead;

        public StubFileSystemOperations(Func<string, Stream> openRead)
        {
            _openRead = openRead;
        }

        public Stream OpenRead(string path)
        {
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
