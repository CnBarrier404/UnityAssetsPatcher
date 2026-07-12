using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Manifests;

public sealed class ModManifestParserTests
{
    /// <summary>
    /// Verifies that match object cannot be empty.
    /// </summary>
    [Fact]
    public void Parse_WhenMatchIsEmpty_ThrowsInvalidOperationException()
    {
        const string patch =
            """
            {
              "type": "Camera",
              "match": {}
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(() => ParsePatch(patch));

        Assert.Contains("cannot be empty", exception.Message);
    }

    /// <summary>
    /// Verifies that set field value must be an object with from and to properties.
    /// </summary>
    [Fact]
    public void Parse_WhenSetValueIsNotObject_ThrowsInvalidOperationException()
    {
        const string patch =
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": {
                "field of view": "not an object"
              }
            }
            """;

        Assert.Throws<InvalidOperationException>(() => ParsePatch(patch));
    }

    /// <summary>
    /// Verifies that set field value must contain a from property.
    /// </summary>
    [Fact]
    public void Parse_WhenSetMissingFrom_ThrowsInvalidOperationException()
    {
        const string patch =
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": {
                "field of view": { "to": 75.0 }
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(() => ParsePatch(patch));

        Assert.Contains("from", exception.Message);
    }

    /// <summary>
    /// Verifies that set field value must contain a to property.
    /// </summary>
    [Fact]
    public void Parse_WhenSetMissingTo_ThrowsInvalidOperationException()
    {
        const string patch =
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": {
                "field of view": { "from": 90.0 }
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(() => ParsePatch(patch));

        Assert.Contains("to", exception.Message);
    }

    /// <summary>
    /// Verifies that add field value must be an array.
    /// </summary>
    [Fact]
    public void Parse_WhenAddValueIsNotArray_ThrowsInvalidOperationException()
    {
        const string patch =
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "add": {
                "m_ValidKeywords.Array": "not an array"
              }
            }
            """;

        Assert.Throws<InvalidOperationException>(() => ParsePatch(patch));
    }

    /// <summary>
    /// Verifies that a manifest without an optional array yields an empty optional group list.
    /// </summary>
    [Fact]
    public void Parse_WhenManifestHasNoOptional_ReturnsEmptyOptionalList()
    {
        ModManifest manifest = ParseManifest(BaseManifest());

        Assert.Empty(manifest.Optional);
    }

    /// <summary>
    /// Verifies that optional groups are parsed with name, description, patches, and copyFiles.
    /// </summary>
    [Fact]
    public void Parse_WhenManifestHasOptionalGroups_ReturnsParsedGroups()
    {
        ModManifest manifest = ParseManifest(
            """
            {
              "name": "Test Mod",
              "author": "Tester",
              "version": "1.0.0",
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
                  "description": "Replaces textures with 4K versions",
                  "targets": [
                    {
                      "file": "sharedassets1.assets",
                      "patches": [
                        { "type": "Material", "match": { "m_Name": "Skin" }, "set": { "m_Floats": { "from": 0, "to": 1 } } }
                      ]
                    }
                  ],
                  "copyFiles": [ { "source": "extras/tex.resource" } ]
                }
              ]
            }
            """);

        ManifestOptionalGroup group = Assert.Single(manifest.Optional);
        Assert.Equal("High-res textures", group.Name);
        Assert.Equal("Replaces textures with 4K versions", group.Description);
        ManifestPatch patch = Assert.Single(group.Patches);
        Assert.Equal("sharedassets1.assets", patch.AssetsFileName);
        ManifestFile file = Assert.Single(group.Files);
        Assert.Equal("extras/tex.resource", file.Source);
    }

    /// <summary>
    /// Verifies that an optional group may declare only copyFiles without any targets.
    /// </summary>
    [Fact]
    public void Parse_WhenOptionalGroupHasOnlyCopyFiles_Succeeds()
    {
        ModManifest manifest = ParseManifest(
            BaseManifestWithOptional(
                """
                { "name": "Bonus", "copyFiles": [ { "source": "extras/bonus.resource" } ] }
                """));

        ManifestOptionalGroup group = Assert.Single(manifest.Optional);
        Assert.Empty(group.Patches);
        Assert.Single(group.Files);
    }

    /// <summary>
    /// Verifies that an optional group must declare at least one patch or copyFiles entry.
    /// </summary>
    [Fact]
    public void Parse_WhenOptionalGroupHasNeitherTargetsNorCopyFiles_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ParseManifest(BaseManifestWithOptional("""{ "name": "Empty" }""")));

        Assert.Contains("at least one", exception.Message);
    }

    /// <summary>
    /// Verifies that an optional group must contain a non-empty name.
    /// </summary>
    [Fact]
    public void Parse_WhenOptionalGroupMissingName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ParseManifest(BaseManifestWithOptional(
                """{ "copyFiles": [ { "source": "extras/bonus.resource" } ] }""")));
    }

    /// <summary>
    /// Verifies that optional group names must be unique, ignoring case.
    /// </summary>
    [Fact]
    public void Parse_WhenOptionalGroupNamesCollideCaseInsensitive_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ParseManifest(BaseManifestWithOptional(
                """
                { "name": "Bonus", "copyFiles": [ { "source": "a/x.resource" } ] },
                { "name": "bonus", "copyFiles": [ { "source": "b/y.resource" } ] }
                """)));

        Assert.Contains("unique", exception.Message);
    }

    private static string BaseManifest()
    {
        return """
               {
                 "name": "Test Mod",
                 "author": "Tester",
                 "version": "1.0.0",
                 "targets": [
                   {
                     "file": "sharedassets0.assets",
                     "patches": [
                       { "type": "Camera", "match": { "m_Name": "Main" }, "set": { "field of view": { "from": 90.0, "to": 75.0 } } }
                     ]
                   }
                 ]
               }
               """;
    }

    private static string BaseManifestWithOptional(string optionalEntries)
    {
        return $$"""
                 {
                   "name": "Test Mod",
                   "author": "Tester",
                   "version": "1.0.0",
                   "targets": [
                     {
                       "file": "sharedassets0.assets",
                       "patches": [
                         { "type": "Camera", "match": { "m_Name": "Main" }, "set": { "field of view": { "from": 90.0, "to": 75.0 } } }
                       ]
                     }
                   ],
                   "optional": [ {{optionalEntries}} ]
                 }
                 """;
    }

    private static ModManifest ParseManifest(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return ModManifestParser.Parse(document.RootElement);
    }

    private static ModManifest ParsePatch(string patch)
    {
        return ParseManifest(
            $$"""
              {
                "name": "Test Mod",
                "author": "Tester",
                "version": "1.0.0",
                "targets": [
                  {
                    "file": "sharedassets0.assets",
                    "patches": [ {{patch}} ]
                  }
                ]
              }
              """);
    }
}
