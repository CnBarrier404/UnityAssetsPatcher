using System.Text;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Workflows;

public sealed class CheckManifestWorkflowTests
{
    private const string ValidManifest = """
                                         {
                                           "$schema": "https://uap.cnbarrier.com/schema-v1.json",
                                           "name": "Test Mod",
                                           "author": "Test Author",
                                           "version": "1.0.0",
                                           "targets": [
                                             {
                                               "file": "sharedassets0.assets",
                                               "patches": [
                                                 {
                                                   "type": "Camera",
                                                   "match": { "m_Name": "Main" }
                                                 }
                                               ]
                                             }
                                           ]
                                         }
                                         """;

    [Fact]
    public async Task RunAsync_WhenManifestIsValid_ReturnsManifestAndLifecycleLogs()
    {
        var fileSystem = new StubFileSystemOperations(_ => StreamFrom(ValidManifest));
        var logger = new RecordingLogger<CheckManifestWorkflow>();
        CheckManifestWorkflow workflow = CreateWorkflow(fileSystem, logger);

        OperationResult<CheckManifestResult> result = await workflow.RunAsync(
            new CheckManifestRequest("manifest.json"),
            TestContext.Current.CancellationToken);

        var success = Assert.IsType<OperationSucceeded<CheckManifestResult>>(result);
        Assert.Equal("Test Mod", success.Value.Manifest.Name);
        Assert.Equal("1.0.0", success.Value.Manifest.Version);
        Assert.Equal([1000, 1001], logger.EventIds);
    }

    [Fact]
    public async Task RunAsync_WhenManifestIsInvalid_ReturnsStructuredFailureAndFailureLog()
    {
        var fileSystem = new StubFileSystemOperations(_ => StreamFrom("{}"));
        var logger = new RecordingLogger<CheckManifestWorkflow>();
        CheckManifestWorkflow workflow = CreateWorkflow(fileSystem, logger);

        OperationResult<CheckManifestResult> result = await workflow.RunAsync(
            new CheckManifestRequest("manifest.json"),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<CheckManifestResult>>(result);
        Assert.Equal(ManifestErrorCodes.MissingProperty, failure.Error.Code);
        Assert.Equal([1000, 1002], logger.EventIds);
    }

    [Fact]
    public async Task RunAsync_WhenSourceDoesNotExist_ReturnsFileNotFound()
    {
        var fileSystem = new StubFileSystemOperations(path => throw new FileNotFoundException(null, path));
        var logger = new RecordingLogger<CheckManifestWorkflow>();
        CheckManifestWorkflow workflow = CreateWorkflow(fileSystem, logger);

        OperationResult<CheckManifestResult> result = await workflow.RunAsync(
            new CheckManifestRequest("missing.json"),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<CheckManifestResult>>(result);
        Assert.Equal(FileErrorCodes.NotFound, failure.Error.Code);
        Assert.Equal("missing.json", failure.Error.Parameters["path"]);
        Assert.Equal([1000, 1002], logger.EventIds);
    }

    [Fact]
    public async Task RunAsync_WhenPackageIsInvalid_ReturnsInvalidArchive()
    {
        var fileSystem = new StubFileSystemOperations(_ => StreamFrom(string.Empty));
        var logger = new RecordingLogger<CheckManifestWorkflow>();
        var archiveFactory =
            new StubModPackageArchiveFactory(_ => throw new InvalidDataException("Invalid test archive."));
        CheckManifestWorkflow workflow = CreateWorkflow(fileSystem, logger, archiveFactory);

        OperationResult<CheckManifestResult> result = await workflow.RunAsync(
            new CheckManifestRequest("mod.zip"),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<CheckManifestResult>>(result);
        Assert.Equal(ModPackageErrorCodes.InvalidArchive, failure.Error.Code);
        Assert.Equal("mod.zip", failure.Error.Parameters["package_path"]);
        Assert.Equal([1000, 1002], logger.EventIds);
    }

    [Fact]
    public async Task RunAsync_WhenDependencyFaults_RethrowsAndLogsFault()
    {
        var fileSystem = new StubFileSystemOperations(_ => throw new InvalidOperationException("Test fault."));
        var logger = new RecordingLogger<CheckManifestWorkflow>();
        CheckManifestWorkflow workflow = CreateWorkflow(fileSystem, logger);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.RunAsync(
                new CheckManifestRequest("manifest.json"),
                TestContext.Current.CancellationToken));

        Assert.Equal("Test fault.", exception.Message);
        Assert.Equal([1000, 1003], logger.EventIds);
    }

    private static CheckManifestWorkflow CreateWorkflow(
        IFileSystemOperations fileSystemOperations,
        ILogger<CheckManifestWorkflow> logger,
        IModPackageArchiveFactory? archiveFactory = null)
    {
        archiveFactory ??= new StubModPackageArchiveFactory(_ =>
            throw new InvalidOperationException("The archive factory should not be called."));
        var archiveService = new ModPackageArchiveService(archiveFactory, fileSystemOperations);
        var sourceReader = new ManifestSourceReader(archiveService, fileSystemOperations);

        return new CheckManifestWorkflow(sourceReader, logger);
    }

    private static Stream StreamFrom(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value));
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

    private sealed class StubModPackageArchiveFactory : IModPackageArchiveFactory
    {
        private readonly Func<string, IModPackageArchive> _openRead;

        public StubModPackageArchiveFactory(Func<string, IModPackageArchive> openRead)
        {
            _openRead = openRead;
        }

        public IModPackageArchive OpenRead(string packagePath)
        {
            return _openRead(packagePath);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<int> EventIds { get; } = [];

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
            EventIds.Add(eventId.Id);
        }
    }
}
