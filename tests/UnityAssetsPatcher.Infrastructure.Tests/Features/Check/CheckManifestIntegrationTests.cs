using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Mods;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Features.Check;

public sealed class CheckManifestIntegrationTests : IDisposable
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

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"UnityAssetsPatcher-CheckManifest-{Guid.NewGuid():N}");

    public CheckManifestIntegrationTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public async Task HandleAsync_WhenSourceIsJsonFile_ReturnsManifest()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "manifest.json");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(sourcePath, ValidManifest, cancellationToken);

        CheckManifestHandler handler = CreateHandler();
        OperationResult<CheckManifestResult> result = await handler.HandleAsync(
            new CheckManifestRequest(sourcePath),
            cancellationToken);

        var success = Assert.IsType<OperationSucceeded<CheckManifestResult>>(result);
        Assert.Equal("Test Mod", success.Value.Manifest.Name);
    }

    [Fact]
    public async Task HandleAsync_WhenSourceIsZipFile_ReturnsNestedManifest()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "mod.ZIP");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (ZipArchive archive = ZipFile.Open(sourcePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("Mod/manifest.json");
            await using Stream output = entry.Open();

            await output.WriteAsync(System.Text.Encoding.UTF8.GetBytes(ValidManifest), cancellationToken);
        }

        CheckManifestHandler handler = CreateHandler();
        OperationResult<CheckManifestResult> result = await handler.HandleAsync(
            new CheckManifestRequest(sourcePath),
            cancellationToken);

        var success = Assert.IsType<OperationSucceeded<CheckManifestResult>>(result);
        Assert.Equal("Test Mod", success.Value.Manifest.Name);
    }

    [Fact]
    public async Task HandleAsync_WhenZipManifestIsMissing_ReturnsInvalidArchiveFailure()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "missing-manifest.zip");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (ZipArchive archive = ZipFile.Open(sourcePath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("payload.bin");
        }

        CheckManifestHandler handler = CreateHandler();
        OperationResult<CheckManifestResult> result = await handler.HandleAsync(
            new CheckManifestRequest(sourcePath),
            cancellationToken);

        var failure = Assert.IsType<OperationFailed<CheckManifestResult>>(result);
        Assert.Equal(ModPackageErrorCodes.InvalidArchive, failure.Error.Code);
        Assert.Equal(sourcePath, failure.Error.Parameters["package_path"]);
    }

    [Fact]
    public async Task HandleAsync_WhenSourceDoesNotExist_ReturnsFileFailure()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "missing.json");
        CheckManifestHandler handler = CreateHandler();

        OperationResult<CheckManifestResult> result = await handler.HandleAsync(
            new CheckManifestRequest(sourcePath),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationFailed<CheckManifestResult>>(result);
        Assert.Equal(FileErrorCodes.NotFound, failure.Error.Code);
        Assert.Equal(sourcePath, failure.Error.Parameters["path"]);
    }

    public void Dispose()
    {
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private static CheckManifestHandler CreateHandler()
    {
        var fileSystemOperations = new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
        var packageReader = new ModPackageReader(fileSystemOperations);

        return new CheckManifestHandler(new ModManifestReader(fileSystemOperations, packageReader));
    }
}
