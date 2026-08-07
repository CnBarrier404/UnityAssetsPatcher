using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Failures;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Infrastructure;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Packages;

public sealed class ModManifestServiceIntegrationTests : IDisposable
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

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"UnityAssetsPatcher-ManifestService-{Guid.NewGuid():N}");

    public ModManifestServiceIntegrationTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenSourceIsJsonFile_ReturnsManifest()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "manifest.json");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(sourcePath, ValidManifest, cancellationToken);

        using ServiceProvider provider = CreateProvider();
        IModManifestService service = provider.GetRequiredService<IModManifestService>();
        ModManifest manifest = await service.ReadManifestAsync(sourcePath, cancellationToken);

        Assert.Equal("Test Mod", manifest.Name);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenSourceIsZipFile_ReturnsNestedManifest()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "mod.ZIP");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (ZipArchive archive = ZipFile.Open(sourcePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("Mod/manifest.json");
            await using Stream output = entry.Open();

            await output.WriteAsync(
                System.Text.Encoding.UTF8.GetBytes(ValidManifest),
                cancellationToken);
        }

        using ServiceProvider provider = CreateProvider();
        IModManifestService service = provider.GetRequiredService<IModManifestService>();
        ModManifest manifest = await service.ReadManifestAsync(sourcePath, cancellationToken);

        Assert.Equal("Test Mod", manifest.Name);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenZipManifestIsMissing_ThrowsPackageException()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "missing-manifest.zip");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (ZipArchive archive = ZipFile.Open(sourcePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("payload.bin");
            await using Stream output = entry.Open();
            byte[] payload = [1, 2, 3];
            await output.WriteAsync(payload, cancellationToken);
        }

        using ServiceProvider provider = CreateProvider();
        IModManifestService service = provider.GetRequiredService<IModManifestService>();
        PackageException exception = await Assert.ThrowsAsync<PackageException>(() =>
            service.ReadManifestAsync(sourcePath, cancellationToken));

        Assert.Equal(ModPackageErrorCodes.ManifestMissing.Value, exception.Code);
        Assert.Equal(sourcePath, exception.Parameters["package_path"]);
    }

    [Fact]
    public async Task ReadManifestAsync_WhenSourceDoesNotExist_ThrowsFileOperationException()
    {
        string sourcePath = Path.Combine(_temporaryDirectory, "missing.json");

        using ServiceProvider provider = CreateProvider();
        IModManifestService service = provider.GetRequiredService<IModManifestService>();
        FileOperationException exception = await Assert.ThrowsAsync<FileOperationException>(() =>
            service.ReadManifestAsync(sourcePath, TestContext.Current.CancellationToken));

        Assert.Equal(FileErrorCodes.NotFound.Value, exception.Code);
        Assert.Equal(sourcePath, exception.Parameters["path"]);
    }

    public void Dispose()
    {
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUnityAssetsPatcherPackageHandling();
        services.AddUnityAssetsPatcherApplication();

        return services.BuildServiceProvider();
    }
}
