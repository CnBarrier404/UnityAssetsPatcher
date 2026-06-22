using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ModManifestOptionalSelectorTests
{
    /// <summary>
    /// Verifies that selecting nothing yields the base manifest with its optional groups cleared.
    /// </summary>
    [Fact]
    public void SelectOptional_WhenNoSelection_ReturnsBaseWithClearedOptional()
    {
        ModManifest manifest = ParseManifest();

        ModManifest effective = manifest.SelectOptional([]);

        Assert.Single(effective.Patches);
        Assert.Single(effective.Files);
        Assert.Empty(effective.Optional);
    }

    /// <summary>
    /// Verifies that a selected optional group contributes its patches and copyFiles to the effective manifest.
    /// </summary>
    [Fact]
    public void SelectOptional_WhenGroupSelected_MergesPatchesAndFiles()
    {
        ModManifest manifest = ParseManifest();

        ModManifest effective = manifest.SelectOptional(["High-res textures"]);

        Assert.Equal(2, effective.Patches.Count);
        Assert.Equal(2, effective.Files.Count);
        Assert.Contains(effective.Patches, patch => patch.AssetsFileName == "sharedassets1.assets");
        Assert.Empty(effective.Optional);
    }

    /// <summary>
    /// Verifies that selection matches optional group names case-insensitively.
    /// </summary>
    [Fact]
    public void SelectOptional_WhenNameDiffersByCase_SelectsGroup()
    {
        ModManifest manifest = ParseManifest();

        ModManifest effective = manifest.SelectOptional(["high-res TEXTURES"]);

        Assert.Equal(2, effective.Patches.Count);
    }

    /// <summary>
    /// Verifies that selecting an unknown optional group name throws.
    /// </summary>
    [Fact]
    public void SelectOptional_WhenNameIsUnknown_Throws()
    {
        ModManifest manifest = ParseManifest();

        var exception = Assert.Throws<InvalidOperationException>(() => manifest.SelectOptional(["Does not exist"]));

        Assert.Contains("Unknown optional group", exception.Message);
    }

    /// <summary>
    /// Verifies that merging base and optional copyFiles that share a destination file name throws.
    /// </summary>
    [Fact]
    public void SelectOptional_WhenSelectionDuplicatesPayloadFileName_Throws()
    {
        ModManifest manifest = ParseManifest();

        var exception = Assert.Throws<InvalidOperationException>(() => manifest.SelectOptional(["Collides"]));

        Assert.Contains("duplicate payload file name", exception.Message);
    }

    private static ModManifest ParseManifest()
    {
        const string json =
            """
            {
              "name": "Test Mod",
              "author": "Tester",
              "version": "1.0.0",
              "copyFiles": [ { "source": "base/shared.resource" } ],
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    { "type": "Camera", "match": { "m_Name": "Main" }, "set": { "field of view": { "from": 90.0, "to": 75.0 } } }
                  ]
                }
              ],
              "optional": [
                {
                  "name": "High-res textures",
                  "targets": [
                    {
                      "file": "sharedassets1.assets",
                      "patches": [
                        { "type": "Material", "match": { "m_Name": "Skin" }, "set": { "m_Floats": { "from": 0, "to": 1 } } }
                      ]
                    }
                  ],
                  "copyFiles": [ { "source": "extras/tex.resource" } ]
                },
                {
                  "name": "Collides",
                  "copyFiles": [ { "source": "extras/shared.resource" } ]
                }
              ]
            }
            """;

        using JsonDocument document = JsonDocument.Parse(json);

        return ModManifestParser.Parse(document.RootElement);
    }
}
