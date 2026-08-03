using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
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
    public void Select_WhenNoGroupIsSelected_ReturnsRequiredContentOnly()
    {
        ModManifest manifest = ParseManifest(ManifestJson);

        ModManifestSelection selection = SelectSuccess(manifest, []);

        Assert.Single(selection.Manifest.Files);
        Assert.Single(selection.Manifest.Patches);
        Assert.Empty(selection.Manifest.OptionalGroups);
        Assert.Empty(selection.AppliedOptionalGroups);
    }

    [Fact]
    public void Select_WhenGroupUsesDifferentCase_MergesContentAndReportsCanonicalName()
    {
        ModManifest manifest = ParseManifest(ManifestJson);

        ModManifestSelection selection = SelectSuccess(manifest, ["bonus CAMERA"]);

        Assert.Equal(2, selection.Manifest.Patches.Count);
        Assert.Contains(selection.Manifest.Patches, patch => patch.AssetsFileName == "sharedassets1.assets");
        Assert.Equal(["Bonus camera"], selection.AppliedOptionalGroups);
    }

    [Fact]
    public void Select_WhenUnknownGroupIsSelected_ReturnsStructuredFailure()
    {
        ModManifest manifest = ParseManifest(ManifestJson);

        OperationResult<ModManifestSelection> result = ModManifestOptionalSelector.Select(manifest, ["Missing"]);
        var failure = Assert.IsType<OperationFailed<ModManifestSelection>>(result);

        Assert.Equal(ManifestErrorCodes.UnknownOptionalGroup, failure.Error.Code);
        Assert.Equal("Missing", failure.Error.Parameters["name"]);
    }

    [Fact]
    public void Select_WhenMergedPayloadFileNamesCollideIgnoringCase_ReturnsStructuredFailure()
    {
        ModManifest manifest = ParseManifest(
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
            """);

        OperationResult<ModManifestSelection> result = ModManifestOptionalSelector.Select(manifest, ["Payload"]);
        var failure = Assert.IsType<OperationFailed<ModManifestSelection>>(result);

        Assert.Equal(ManifestErrorCodes.PayloadConflict, failure.Error.Code);
        Assert.Equal("PAYLOAD.RESOURCE", failure.Error.Parameters["file_name"]);
    }

    [Fact]
    public void Select_WhenPatchOnlyGroupIsRepeated_PreservesLegacyDuplicateMerge()
    {
        ModManifest manifest = ParseManifest(ManifestJson);

        ModManifestSelection selection = SelectSuccess(manifest, ["Bonus camera", "BONUS CAMERA"]);

        Assert.Equal(3, selection.Manifest.Patches.Count);
        Assert.Equal(["Bonus camera"], selection.AppliedOptionalGroups);
    }

    private static ModManifest ParseManifest(string json)
    {
        OperationResult<ModManifest> result = ModManifestParser.Parse(json);
        var success = Assert.IsType<OperationSucceeded<ModManifest>>(result);

        return success.Value;
    }

    private static ModManifestSelection SelectSuccess(ModManifest manifest, IReadOnlyList<string> selectedNames)
    {
        OperationResult<ModManifestSelection> result = ModManifestOptionalSelector.Select(manifest, selectedNames);
        var success = Assert.IsType<OperationSucceeded<ModManifestSelection>>(result);

        return success.Value;
    }
}
