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
    public void Open_WhenPackageReaderFaults_PropagatesOriginalException()
    {
        var expected = new FileNotFoundException("missing", "missing.zip");
        var packageReader = new StubPackageReader((_, _, _) => throw expected);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => OpenPackage(packageReader));

        Assert.Same(expected, exception);
    }

    [Fact]
    public void CopyPayloadFile_WhenCalled_CopiesExtractedEntry()
    {
        var content = new PackageContent(
            Encoding.UTF8.GetBytes(ValidManifest),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["payload.bin"] = "payload.extracted",
            });
        var packageReader = new StubPackageReader(content);
        var fileSystem = new StubFileSystemOperations();
        using ModPackage package = Assert.IsType<OperationSucceeded<ModPackage>>(
            OpenPackage(packageReader, fileSystem)).Value;

        package.CopyPayloadFile("PAYLOAD.BIN", "payload.output");

        Assert.Equal("payload.extracted", fileSystem.CopiedSourcePath);
        Assert.Equal("payload.output", fileSystem.CopyDestinationPath);
    }

    [Fact]
    public async Task ReadAsync_WhenPackageManifestJsonIsInvalid_PropagatesJsonException()
    {
        var packageReader = new StubPackageReader(Encoding.UTF8.GetBytes("{"));
        var reader = new ModManifestReader(new StubFileSystemOperations(), packageReader);

        _ = await Assert.ThrowsAnyAsync<JsonException>(() =>
            reader.ReadAsync("mod.zip", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_WhenSourceIsPackage_OpensNormalizedPath()
    {
        var packageReader = new StubPackageReader(Encoding.UTF8.GetBytes(ValidManifest));
        var reader = new ModManifestReader(new StubFileSystemOperations(), packageReader);

        _ = await reader.ReadAsync("mod.zip", TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath("mod.zip"), packageReader.OpenedPath);
    }

    private static OperationResult<ModPackage> OpenPackage(
        IPackageReader packageReader,
        IFileSystemOperations? fileSystemOperations = null)
    {
        return ModPackage.Open(
            "mod.zip",
            [],
            packageReader,
            fileSystemOperations ?? new StubFileSystemOperations(),
            new StepTimer());
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        public string? CopiedSourcePath { get; private set; }
        public string? CopyDestinationPath { get; private set; }

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
            CopiedSourcePath = sourcePath;
            CopyDestinationPath = destinationPath;
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
