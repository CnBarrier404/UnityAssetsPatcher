using System.Text;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Manifests;

public sealed class ModManifestOptionalSelectorTests
{
    private const string ManifestJson =
        """
        {
          "$schema": "https://uap.cnbarrier.com/schema-v1.json",
          "name": "Test Mod",
          "author": "Test Author",
          "version": "1.0.0",
          "copyFiles": [ { "source": "base/base.resource" } ],
          "targets": [
            {
              "file": "sharedassets0.assets",
              "patches": [ { "type": "Camera", "match": { "m_Name": "Main" } } ]
            }
          ],
          "optional": [
            {
              "name": "Bonus camera",
              "targets": [
                {
                  "file": "sharedassets1.assets",
                  "patches": [ { "type": "Camera", "match": { "m_Name": "Bonus" } } ]
                }
              ]
            },
            {
              "name": "Payload",
              "copyFiles": [ { "source": "extra/payload.resource" } ]
            }
          ]
        }
        """;

    [Fact]
    public void Open_WhenNoGroupIsSelected_ReturnsRequiredContentOnly()
    {
        using ModPackage package = OpenPackage(ManifestJson, []);

        Assert.Single(package.Manifest.Files);
        Assert.Single(package.Manifest.Patches);
        Assert.Empty(package.Manifest.OptionalGroups);
        Assert.Empty(package.AppliedOptionalGroups);
    }

    [Fact]
    public void Open_WhenGroupUsesDifferentCase_MergesContentAndReportsCanonicalName()
    {
        using ModPackage package = OpenPackage(ManifestJson, ["bonus CAMERA"]);

        Assert.Equal(2, package.Manifest.Patches.Count);
        Assert.Contains(package.Manifest.Patches, patch => patch.AssetsFileName == "sharedassets1.assets");
        Assert.Equal(["Bonus camera"], package.AppliedOptionalGroups);
    }

    [Fact]
    public void Open_WhenUnknownGroupIsSelected_ThrowsOperationFailure()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            OpenPackage(ManifestJson, ["Missing"]));

        Assert.Contains("manifest.unknown_optional_group", exception.Message);
        Assert.Contains("name=Missing", exception.Message);
    }

    [Fact]
    public void Open_WhenMergedPayloadFileNamesCollideIgnoringCase_ThrowsOperationFailure()
    {
        const string manifest =
            """
            {
              "$schema": "https://uap.cnbarrier.com/schema-v1.json",
              "name": "Test Mod",
              "author": "Test Author",
              "version": "1.0.0",
              "copyFiles": [ { "source": "base/payload.resource" } ],
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [ { "type": "Camera", "match": { "m_Name": "Main" } } ]
                }
              ],
              "optional": [
                {
                  "name": "Payload",
                  "copyFiles": [ { "source": "extra/PAYLOAD.RESOURCE" } ]
                }
              ]
            }
            """;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            OpenPackage(manifest, ["Payload"]));

        Assert.Contains("manifest.payload_conflict", exception.Message);
        Assert.Contains("file_name=PAYLOAD.RESOURCE", exception.Message);
    }

    [Fact]
    public void Open_WhenPatchOnlyGroupIsRepeated_PreservesLegacyDuplicateMerge()
    {
        using ModPackage package = OpenPackage(ManifestJson, ["Bonus camera", "BONUS CAMERA"]);

        Assert.Equal(3, package.Manifest.Patches.Count);
        Assert.Equal(["Bonus camera"], package.AppliedOptionalGroups);
    }

    private static ModPackage OpenPackage(string manifest, IReadOnlyList<string> selectedNames)
    {
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifest);
        var fileSystemOperations = new StubFileSystemOperations();
        var archiveService = new ModPackageArchiveService(
            new StubModPackageArchiveFactory(manifestBytes),
            fileSystemOperations);

        return ModPackage.Open(
            "mod.zip",
            selectedNames,
            archiveService,
            fileSystemOperations,
            new StepTimer());
    }

    private sealed class StubModPackageArchiveFactory : IModPackageArchiveFactory
    {
        private readonly byte[] _manifest;

        public StubModPackageArchiveFactory(byte[] manifest)
        {
            _manifest = manifest;
        }

        public IModPackageArchive OpenRead(string packagePath)
        {
            return new StubModPackageArchive(packagePath, _manifest);
        }
    }

    private sealed class StubModPackageArchive : IModPackageArchive
    {
        private readonly byte[] _manifest;

        public string PackagePath { get; }

        public IReadOnlyList<PackageEntryInfo> Entries { get; }

        public StubModPackageArchive(string packagePath, byte[] manifest)
        {
            PackagePath = packagePath;
            _manifest = manifest;
            Entries = [new PackageEntryInfo(new PackageEntryId(1), "manifest.json", manifest.Length, false)];
        }

        public Stream OpenEntry(PackageEntryId entryId)
        {
            return entryId.Value == 1
                ? new MemoryStream(_manifest, writable: false)
                : throw new InvalidOperationException($"Unknown archive entry: {entryId.Value}.");
        }

        public void Dispose() { }
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
