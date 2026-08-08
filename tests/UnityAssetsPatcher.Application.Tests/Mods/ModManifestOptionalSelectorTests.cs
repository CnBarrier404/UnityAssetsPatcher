using System.Text;
using System.IO.Compression;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Mods;

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

        Assert.Single(package.EffectiveManifest.Files);
        Assert.Single(package.EffectiveManifest.Patches);
        Assert.Empty(package.EffectiveManifest.OptionalGroups);
        Assert.Equal(2, package.SourceManifest.OptionalGroups.Count);
        Assert.Empty(package.AppliedOptionalGroups);
    }

    [Fact]
    public void Open_WhenGroupUsesDifferentCase_MergesContentAndReportsCanonicalName()
    {
        using ModPackage package = OpenPackage(ManifestJson, ["bonus CAMERA"]);

        Assert.Equal(2, package.EffectiveManifest.Patches.Count);
        Assert.Contains(
            package.EffectiveManifest.Patches,
            patch => patch.AssetsFileName == "sharedassets1.assets");
        Assert.Equal(["Bonus camera"], package.AppliedOptionalGroups);
    }

    [Fact]
    public void Open_WhenUnknownGroupIsSelected_ReturnsStructuredFailure()
    {
        OperationResult<ModPackage> result = OpenPackageResult(ManifestJson, ["Missing"]);

        var failure = Assert.IsType<OperationFailed<ModPackage>>(result);
        Assert.Equal(ManifestErrorCodes.UnknownOptionalGroup, failure.Error.Code);
        Assert.Equal("Missing", failure.Error.Parameters["name"]);
    }

    [Fact]
    public void Open_WhenMergedPayloadFileNamesCollideIgnoringCase_ReturnsStructuredFailure()
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

        OperationResult<ModPackage> result = OpenPackageResult(manifest, ["Payload"]);

        var failure = Assert.IsType<OperationFailed<ModPackage>>(result);
        Assert.Equal(ManifestErrorCodes.PayloadConflict, failure.Error.Code);
        Assert.Equal("PAYLOAD.RESOURCE", failure.Error.Parameters["file_name"]);
    }

    [Fact]
    public void Open_WhenPatchOnlyGroupIsRepeated_PreservesLegacyDuplicateMerge()
    {
        using ModPackage package = OpenPackage(ManifestJson, ["Bonus camera", "BONUS CAMERA"]);

        Assert.Equal(3, package.EffectiveManifest.Patches.Count);
        Assert.Equal(["Bonus camera"], package.AppliedOptionalGroups);
    }

    private static ModPackage OpenPackage(string manifest, IReadOnlyList<string> selectedNames)
    {
        OperationResult<ModPackage> result = OpenPackageResult(manifest, selectedNames);
        var success = Assert.IsType<OperationSucceeded<ModPackage>>(result);

        return success.Value;
    }

    private static OperationResult<ModPackage> OpenPackageResult(
        string manifest,
        IReadOnlyList<string> selectedNames)
    {
        byte[] archiveBytes = CreateArchive(manifest);
        var fileSystemOperations = new StubFileSystemOperations(archiveBytes);

        return ModPackage.Open(
            "mod.zip",
            selectedNames,
            fileSystemOperations,
            new StepTimer());
    }

    private static byte[] CreateArchive(string manifest)
    {
        using var output = new MemoryStream();

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("manifest.json");
            using Stream stream = entry.Open();
            byte[] manifestBytes = Encoding.UTF8.GetBytes(manifest);

            stream.Write(manifestBytes);
        }

        return output.ToArray();
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        private readonly byte[] _archiveBytes;

        public StubFileSystemOperations(byte[] archiveBytes)
        {
            _archiveBytes = archiveBytes;
        }

        public Stream OpenRead(string path)
        {
            return new MemoryStream(_archiveBytes, writable: false);
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
