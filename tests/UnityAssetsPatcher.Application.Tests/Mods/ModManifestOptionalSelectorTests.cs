using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task OpenAsync_WhenNoGroupIsSelected_ReturnsRequiredContentOnly()
    {
        using ModPackage package = await OpenPackageAsync(ManifestJson, []);

        Assert.Single(package.EffectiveManifest.Files);
        Assert.Single(package.EffectiveManifest.Patches);
        Assert.Empty(package.EffectiveManifest.OptionalGroups);
        Assert.Equal(2, package.SourceManifest.OptionalGroups.Count);
        Assert.Empty(package.AppliedOptionalGroups);
    }

    [Fact]
    public async Task OpenAsync_WhenGroupUsesDifferentCase_MergesContentAndReportsCanonicalName()
    {
        using ModPackage package = await OpenPackageAsync(ManifestJson, ["bonus CAMERA"]);

        Assert.Equal(2, package.EffectiveManifest.Patches.Count);
        Assert.Contains(
            package.EffectiveManifest.Patches,
            patch => patch.AssetsFileName == "sharedassets1.assets");
        Assert.Equal(["Bonus camera"], package.AppliedOptionalGroups);
    }

    [Fact]
    public async Task OpenAsync_WhenUnknownGroupIsSelected_ReturnsStructuredFailure()
    {
        var result = await OpenPackageResultAsync(ManifestJson, ["Missing"]);

        var failure = Assert.IsType<OperationFailed<ModPackage>>(result);
        Assert.Equal(ManifestErrorCodes.UnknownOptionalGroup, failure.Error.Code);
        Assert.Equal("Missing", failure.Error.Parameters["name"]);
    }

    [Fact]
    public async Task OpenAsync_WhenMergedPayloadFileNamesCollideIgnoringCase_ReturnsStructuredFailure()
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

        var result = await OpenPackageResultAsync(manifest, ["Payload"]);

        var failure = Assert.IsType<OperationFailed<ModPackage>>(result);
        Assert.Equal(ManifestErrorCodes.PayloadConflict, failure.Error.Code);
        Assert.Equal("PAYLOAD.RESOURCE", failure.Error.Parameters["file_name"]);
    }

    [Fact]
    public async Task OpenAsync_WhenPatchOnlyGroupIsRepeated_PreservesLegacyDuplicateMerge()
    {
        using ModPackage package = await OpenPackageAsync(ManifestJson, ["Bonus camera", "BONUS CAMERA"]);

        Assert.Equal(3, package.EffectiveManifest.Patches.Count);
        Assert.Equal(["Bonus camera"], package.AppliedOptionalGroups);
    }

    private static async Task<ModPackage> OpenPackageAsync(
        string manifest,
        IReadOnlyList<string> selectedNames)
    {
        var result = await OpenPackageResultAsync(manifest, selectedNames);
        var success = Assert.IsType<OperationSucceeded<ModPackage>>(result);

        return success.Value;
    }

    private static Task<OperationResult<ModPackage>> OpenPackageResultAsync(
        string manifest,
        IReadOnlyList<string> selectedNames)
    {
        var session = new StubModArchiveSession(
            Encoding.UTF8.GetBytes(manifest),
            ("base/base.resource", Array.Empty<byte>()),
            ("extra/payload.resource", Array.Empty<byte>()));
        var archiveReader = new StubModArchiveReader(session);
        var fileSystemOperations = new StubFileSystemOperations();
        var packageReader = new ModPackageReader(
            archiveReader,
            fileSystemOperations,
            NullLoggerFactory.Instance);

        return packageReader.OpenAsync(
            "mod.zip",
            selectedNames,
            new StepTimer(),
            TestContext.Current.CancellationToken);
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
