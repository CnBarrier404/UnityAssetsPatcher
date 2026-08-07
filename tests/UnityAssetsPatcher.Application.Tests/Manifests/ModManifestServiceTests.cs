using System.Text;
using UnityAssetsPatcher.Application.Failures;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Packages;
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
    public void ReadManifestAsync_WhenManifestIsValid_ReturnsManifest()
    {
        using ManifestServiceTestHost host = ManifestServiceTestHost.FromText(ValidManifest);

        ModManifest manifest = host.Read();

        Assert.Equal("Test Mod", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
    }

    [Fact]
    public void ReadManifestAsync_WhenManifestIsInvalid_ThrowsManifestException()
    {
        using ManifestServiceTestHost host = ManifestServiceTestHost.FromText("{}");

        ManifestException exception = Assert.Throws<ManifestException>(() => host.Read());

        Assert.Equal(ManifestErrorCodes.MissingProperty.Value, exception.Code);
    }

    [Fact]
    public void ReadManifestAsync_WhenSourceDoesNotExist_ThrowsFileOperationException()
    {
        using ManifestServiceTestHost host =
            ManifestServiceTestHost.Create(path => throw new FileNotFoundException(null, path));

        FileOperationException exception = Assert.Throws<FileOperationException>(() =>
            host.Read("missing.json"));

        Assert.Equal(FileErrorCodes.NotFound.Value, exception.Code);
        Assert.Equal("missing.json", exception.Parameters["path"]);
    }

    [Fact]
    public void ReadManifestAsync_WhenPackageIsInvalid_ThrowsPackageException()
    {
        using ManifestServiceTestHost host = ManifestServiceTestHost.Create(
            _ => new MemoryStream(),
            _ => throw new InvalidDataException("Invalid test archive."));

        PackageException exception = Assert.Throws<PackageException>(() => host.Read("mod.zip"));

        Assert.Equal(ModPackageErrorCodes.InvalidArchive.Value, exception.Code);
        Assert.Equal("mod.zip", exception.Parameters["package_path"]);
    }

    [Fact]
    public void ReadManifestAsync_WhenDependencyFaults_RethrowsUnexpectedException()
    {
        using ManifestServiceTestHost host =
            ManifestServiceTestHost.Create(_ => throw new InvalidOperationException("Test fault."));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => host.Read());

        Assert.Equal("Test fault.", exception.Message);
    }

    [Fact]
    public void ReadManifestAsync_WhenSourceContainsUtf8ByteOrderMark_ReturnsManifest()
    {
        byte[] bytes = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(ValidManifest)];
        using ManifestServiceTestHost host = ManifestServiceTestHost.FromBytes(bytes);

        ModManifest manifest = host.Read();

        Assert.Equal("Test Mod", manifest.Name);
    }
}
