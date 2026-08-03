using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Packages;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Packages;

public sealed class ManifestSourceReaderTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"UnityAssetsPatcher-ManifestReader-{Guid.NewGuid():N}");

    public ManifestSourceReaderTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public async Task ReadAsync_WhenSourceIsJsonFile_ReturnsFileBytes()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "manifest.json");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(sourcePath, "{\"name\":\"Example\"}", cancellationToken);

        ManifestSourceReader reader = CreateReader();

        OperationResult<byte[]> result = await reader.ReadAsync(sourcePath, cancellationToken);

        var success = Assert.IsType<OperationSucceeded<byte[]>>(result);
        Assert.Equal("{\"name\":\"Example\"}", Encoding.UTF8.GetString(success.Value));
    }

    [Fact]
    public async Task ReadAsync_WhenSourceIsZipFile_ReturnsNestedManifestBytes()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "mod.ZIP");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (ZipArchive archive = ZipFile.Open(sourcePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("Mod/manifest.json");
            await using Stream output = entry.Open();

            await output.WriteAsync("{}"u8.ToArray(), cancellationToken);
        }

        ManifestSourceReader reader = CreateReader();

        OperationResult<byte[]> result = await reader.ReadAsync(sourcePath, cancellationToken);

        var success = Assert.IsType<OperationSucceeded<byte[]>>(result);
        Assert.Equal("{}", Encoding.UTF8.GetString(success.Value));
    }

    [Fact]
    public async Task ReadAsync_WhenSourceDoesNotExist_ThrowsStandardException()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "missing.json");
        ManifestSourceReader reader = CreateReader();

        FileNotFoundException exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            reader.ReadAsync(sourcePath, TestContext.Current.CancellationToken));

        Assert.Equal(sourcePath, exception.FileName);
    }

    public void Dispose()
    {
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private static ManifestSourceReader CreateReader()
    {
        var fileSystemOperations = new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
        var archiveFactory = new ModPackageArchiveFactory(
            fileSystemOperations,
            NullLogger<ModPackageArchiveFactory>.Instance);
        var archiveService = new ModPackageArchiveService(archiveFactory, fileSystemOperations);

        return new ManifestSourceReader(archiveService, fileSystemOperations);
    }
}
