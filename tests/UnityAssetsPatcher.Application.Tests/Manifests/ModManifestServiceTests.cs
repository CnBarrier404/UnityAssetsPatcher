using System.Text;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Manifests;

public sealed class ModManifestServiceTests
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
    public async Task ReadManifestAsync_WhenManifestIsValid_ReturnsManifest()
    {
        var fileSystem = new StubFileSystemOperations(_ => StreamFrom(ValidManifest));
        ModManifestService service = CreateService(fileSystem);

        ModManifest manifest = await service.ReadManifestAsync(
            "manifest.json",
            TestContext.Current.CancellationToken);

        Assert.Equal("Test Mod", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenManifestIsInvalid_ThrowsManifestException()
    {
        var fileSystem = new StubFileSystemOperations(_ => StreamFrom("{}"));
        ModManifestService service = CreateService(fileSystem);

        ManifestException exception = await Assert.ThrowsAsync<ManifestException>(() =>
            service.ReadManifestAsync(
                "manifest.json",
                TestContext.Current.CancellationToken));

        Assert.Equal(ManifestErrorCodes.MissingProperty.Value, exception.Code);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenSourceDoesNotExist_ThrowsFileOperationException()
    {
        var fileSystem = new StubFileSystemOperations(path => throw new FileNotFoundException(null, path));
        ModManifestService service = CreateService(fileSystem);

        FileOperationException exception = await Assert.ThrowsAsync<FileOperationException>(() =>
            service.ReadManifestAsync(
                "missing.json",
                TestContext.Current.CancellationToken));

        Assert.Equal(FileErrorCodes.NotFound.Value, exception.Code);
        Assert.Equal("missing.json", exception.Parameters["path"]);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenPackageIsInvalid_ThrowsPackageException()
    {
        var fileSystem = new StubFileSystemOperations(_ => StreamFrom(string.Empty));
        var archiveFactory = new StubModPackageArchiveFactory(_ =>
            throw new InvalidDataException("Invalid test archive."));
        ModManifestService service = CreateService(fileSystem, archiveFactory);

        PackageException exception = await Assert.ThrowsAsync<PackageException>(() =>
            service.ReadManifestAsync(
                "mod.zip",
                TestContext.Current.CancellationToken));

        Assert.Equal(ModPackageErrorCodes.InvalidArchive.Value, exception.Code);
        Assert.Equal("mod.zip", exception.Parameters["package_path"]);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenDependencyFaults_RethrowsUnexpectedException()
    {
        var fileSystem = new StubFileSystemOperations(_ => throw new InvalidOperationException("Test fault."));
        ModManifestService service = CreateService(fileSystem);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReadManifestAsync(
                "manifest.json",
                TestContext.Current.CancellationToken));

        Assert.Equal("Test fault.", exception.Message);
    }

    private static ModManifestService CreateService(
        IFileSystemOperations fileSystemOperations,
        IModPackageArchiveFactory? archiveFactory = null)
    {
        archiveFactory ??= new StubModPackageArchiveFactory(_ =>
            throw new InvalidOperationException("The archive factory should not be called."));
        var archiveService = new ModPackageArchiveService(archiveFactory, fileSystemOperations);
        var sourceReader = new ManifestSourceReader(archiveService, fileSystemOperations);

        return new ModManifestService(sourceReader);
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
}
