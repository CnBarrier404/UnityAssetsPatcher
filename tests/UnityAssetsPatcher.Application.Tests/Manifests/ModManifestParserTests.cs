using System.Text.Json;
using System.Text.Json.Nodes;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Manifests;

public sealed class ModManifestParserTests
{
    private const string SchemaUri = "https://uap.cnbarrier.com/schema-v1.json";

    [Fact]
    public void Parse_WhenManifestContainsSupportedContent_ReturnsNormalizedManifest()
    {
        string json = CreateManifest(
            """
            {
              "description": "Example description",
              "game": "Example Game",
              "copyFiles": [
                { "source": "resources/mod.resource" }
              ],
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    {
                      "type": "Camera",
                      "match": { "m_Name": "Main Camera" },
                      "set": {
                        "field of view": { "from": 60.0, "to": 75.0 }
                      },
                      "add": {
                        "m_ValidKeywords.Array": ["_EMISSION"]
                      }
                    },
                    {
                      "type": "AudioClip",
                      "match": { "m_Name": "Example Clip" },
                      "replaceAsset": {
                        "fromFile": "resources/mod.assets",
                        "matchField": "m_Name"
                      }
                    },
                    {
                      "type": "Material",
                      "match": { "m_Name": "Target" },
                      "copyAsset": {
                        "from": {
                          "type": "Material",
                          "match": { "m_Name": "Source" }
                        }
                      }
                    },
                    {
                      "type": "GameObject",
                      "match": { "m_Name": "Object" },
                      "componentType": "Transform"
                    }
                  ]
                }
              ],
              "optional": [
                {
                  "name": "Bonus",
                  "description": "Optional content",
                  "copyFiles": [ { "source": "extras/bonus.resource" } ]
                }
              ]
            }
            """);

        ModManifest manifest = ParseSuccess(json);

        Assert.Equal(SchemaUri, manifest.Schema);
        Assert.Equal("Test Mod", manifest.Name);
        Assert.Equal("Test Author", manifest.Author);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("Example description", manifest.Description);
        Assert.Equal("Example Game", manifest.Game);
        Assert.Equal("resources/mod.resource", Assert.Single(manifest.Files).Source);
        Assert.Equal(4, manifest.Patches.Count);

        ModPatch fieldPatch = manifest.Patches[0];
        Assert.Equal("sharedassets0.assets", fieldPatch.AssetsFileName);
        Assert.Equal("Camera", fieldPatch.AssetTypeName);
        Assert.Equal("Main Camera", fieldPatch.Match["m_Name"].GetString());
        Assert.Equal("field of view", Assert.Single(fieldPatch.SetOperations).FieldPath);
        Assert.Equal("m_ValidKeywords.Array", Assert.Single(fieldPatch.AddOperations).FieldPath);

        ModReplaceAsset replacement = Assert.IsType<ModReplaceAsset>(manifest.Patches[1].ReplaceAsset);
        Assert.Equal("resources/mod.assets", replacement.SourceAssetsFile);
        Assert.Equal("m_Name", replacement.MatchFieldPath);

        ModCopyAsset copy = Assert.IsType<ModCopyAsset>(manifest.Patches[2].CopyAsset);
        Assert.Equal("Material", copy.AssetTypeName);
        Assert.Equal("Source", copy.Match["m_Name"].GetString());
        Assert.Equal("Transform", manifest.Patches[3].ComponentTypeName);

        ModOptionalGroup optional = Assert.Single(manifest.OptionalGroups);
        Assert.Equal("Bonus", optional.Name);
        Assert.Equal("Optional content", optional.Description);
        Assert.Equal("extras/bonus.resource", Assert.Single(optional.Files).Source);
    }

    [Fact]
    public void Parse_WhenJsonIsInvalid_ReturnsStructuredFailure()
    {
        OperationError error = ParseFailure("{");

        Assert.Equal(ManifestErrorCodes.InvalidJson, error.Code);
        Assert.Contains("line_number", error.Parameters.Keys);
        Assert.Contains("byte_position", error.Parameters.Keys);
    }

    [Fact]
    public void Parse_WhenUtf8JsonHasByteOrderMark_ReturnsManifest()
    {
        byte[] json = [0xef, 0xbb, 0xbf, .. System.Text.Encoding.UTF8.GetBytes(CreateManifest("{}"))];
        OperationResult<ModManifest> result = ModManifestParser.Parse(json);

        var success = Assert.IsType<OperationSucceeded<ModManifest>>(result);
        Assert.Equal("Test Mod", success.Value.Name);
    }

    [Theory]
    [InlineData("{ \"$schema\": 1 }", "manifest.invalid_property_type")]
    [InlineData("{ \"$schema\": \"https://uap.cnbarrier.com/schema-v2.json\" }", "manifest.unsupported_schema")]
    public void Parse_WhenSchemaIsInvalid_ReturnsExpectedFailure(string fragment, string expectedCode)
    {
        OperationError error = ParseFailure(CreateManifest(fragment));

        Assert.Equal(expectedCode, error.Code.Value);
    }

    [Fact]
    public void Parse_WhenSchemaIsMissing_ReturnsFailure()
    {
        OperationError error = ParseFailure(
            """
            {
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
            """);

        Assert.Equal(ManifestErrorCodes.MissingProperty, error.Code);
        Assert.Equal("$schema", error.Parameters["property"]);
    }

    [Theory]
    [InlineData("name", "null", "manifest.invalid_property_type")]
    [InlineData("name", "\"\"", "manifest.invalid_value")]
    [InlineData("author", "42", "manifest.invalid_property_type")]
    [InlineData("version", "true", "manifest.invalid_property_type")]
    public void Parse_WhenRequiredMetadataIsInvalid_ReturnsExpectedFailure(
        string propertyName,
        string propertyValue,
        string expectedCode)
    {
        string json = CreateManifest($$"""
                                       {
                                         "{{propertyName}}": {{propertyValue}}
                                       }
                                       """);

        OperationError error = ParseFailure(json);

        Assert.Equal(expectedCode, error.Code.Value);
        Assert.Equal(propertyName, error.Parameters["property"]);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("author")]
    [InlineData("version")]
    public void Parse_WhenRequiredMetadataIsMissing_ReturnsFailure(string propertyName)
    {
        JsonObject manifest = JsonNode.Parse(CreateManifest("{}"))!.AsObject();
        manifest.Remove(propertyName);

        OperationError error = ParseFailure(manifest.ToJsonString());

        Assert.Equal(ManifestErrorCodes.MissingProperty, error.Code);
        Assert.Equal(propertyName, error.Parameters["property"]);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void Parse_WhenGameIsInvalid_ReturnsFailure(string value)
    {
        OperationError error = ParseFailure(CreateManifest($$"""
                                                             {
                                                               "game": {{value}}
                                                             }
                                                             """));

        Assert.True(error.Code == ManifestErrorCodes.InvalidValue ||
                    error.Code == ManifestErrorCodes.InvalidPropertyType);
        Assert.Equal("game", error.Parameters["property"]);
    }

    [Fact]
    public void Parse_WhenDescriptionIsEmpty_ReturnsEmptyDescription()
    {
        ModManifest manifest = ParseSuccess(CreateManifest(
            """
            {
              "description": ""
            }
            """));

        Assert.Equal(string.Empty, manifest.Description);
    }

    [Fact]
    public void Parse_WhenDescriptionIsNotString_ReturnsFailure()
    {
        OperationError error = ParseFailure(CreateManifest(
            """
            {
              "description": 42
            }
            """));

        Assert.Equal(ManifestErrorCodes.InvalidPropertyType, error.Code);
        Assert.Equal("description", error.Parameters["property"]);
    }

    [Theory]
    [InlineData("[]", "manifest.invalid_value")]
    [InlineData("{}", "manifest.invalid_property_type")]
    public void Parse_WhenTargetsAreInvalid_ReturnsExpectedFailure(string targets, string expectedCode)
    {
        OperationError error = ParseFailure(CreateManifest($$"""
                                                             {
                                                               "targets": {{targets}}
                                                             }
                                                             """));

        Assert.Equal(expectedCode, error.Code.Value);
    }

    [Fact]
    public void Parse_WhenSchemaConstraintFails_ReturnsSchemaLocationDetails()
    {
        OperationError error = ParseFailure(CreateManifest(
            """
            {
              "targets": []
            }
            """));

        Assert.Equal(ManifestErrorCodes.InvalidValue, error.Code);
        Assert.Equal("minItems", error.Parameters["keyword"]);
        Assert.Equal("/targets", error.Parameters["instance_path"]);
        Assert.Contains("/properties/targets", Assert.IsType<string>(error.Parameters["schema_path"]));
    }

    [Fact]
    public void Parse_WhenTargetPatchesAreEmpty_ReturnsFailure()
    {
        OperationError error = ParseFailure(CreateManifest(
            """
            {
              "targets": [
                { "file": "sharedassets0.assets", "patches": [] }
              ]
            }
            """));

        Assert.Equal(ManifestErrorCodes.InvalidValue, error.Code);
        Assert.Equal("patches", error.Parameters["property"]);
    }

    [Fact]
    public void Parse_WhenMatchIsEmpty_ReturnsFailure()
    {
        OperationError error = ParseFailure(CreateManifest(
            """
            {
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [ { "type": "Camera", "match": {} } ]
                }
              ]
            }
            """));

        Assert.Equal(ManifestErrorCodes.InvalidValue, error.Code);
        Assert.Equal("Manifest patch match", error.Parameters["owner"]);
    }

    [Fact]
    public void Parse_WhenMatchContainsDuplicateProperty_ReturnsFailure()
    {
        OperationError error = ParseFailure(
            """
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
                      "match": { "m_Name": "First", "m_Name": "Second" }
                    }
                  ]
                }
              ]
            }
            """);

        Assert.Equal(ManifestErrorCodes.DuplicateProperty, error.Code);
        Assert.Equal("Manifest patch match", error.Parameters["owner"]);
        Assert.Equal("m_Name", error.Parameters["property"]);
    }

    [Theory]
    [InlineData("42", "manifest.invalid_property_type")]
    [InlineData("{}", "manifest.missing_property")]
    [InlineData("{ \"from\": 1 }", "manifest.missing_property")]
    public void Parse_WhenSetOperationIsInvalid_ReturnsExpectedFailure(string operation, string expectedCode)
    {
        string json = ManifestWithPatchProperty($$"""
                                                  "set": { "field": {{operation}} }
                                                  """);

        OperationError error = ParseFailure(json);

        Assert.Equal(expectedCode, error.Code.Value);
    }

    [Fact]
    public void Parse_WhenAddOperationIsNotArray_ReturnsFailure()
    {
        OperationError error = ParseFailure(ManifestWithPatchProperty(
            """
            "add": { "field": "value" }
            """));

        Assert.Equal(ManifestErrorCodes.InvalidPropertyType, error.Code);
        Assert.Equal("array", error.Parameters["expected"]);
    }

    [Theory]
    [InlineData("../payload.resource")]
    [InlineData("/payload.resource")]
    [InlineData("resources/../payload.resource")]
    [InlineData("resources//payload.resource")]
    public void Parse_WhenCopyFilePathIsUnsafe_ReturnsFailure(string source)
    {
        OperationError error = ParseFailure(CreateManifest($$"""
                                                             {
                                                               "copyFiles": [ { "source": "{{source}}" } ]
                                                             }
                                                             """));

        Assert.Equal(ManifestErrorCodes.InvalidPath, error.Code);
        Assert.Equal(source, error.Parameters["path"]);
    }

    [Theory]
    [InlineData("C:/Users/victim/mod.assets")]
    [InlineData("../mod.assets")]
    [InlineData("/mod.assets")]
    [InlineData("resources/../mod.assets")]
    [InlineData("resources//mod.assets")]
    public void Parse_WhenReplacementSourcePathIsUnsafe_ReturnsFailure(string source)
    {
        string sourceJson = JsonSerializer.Serialize(source);
        OperationError error = ParseFailure(ManifestWithPatchProperty($$"""
                                                                        "replaceAsset": {
                                                                          "fromFile": {{sourceJson}},
                                                                          "matchField": "m_Name"
                                                                        }
                                                                        """));

        Assert.Equal(ManifestErrorCodes.InvalidPath, error.Code);
        Assert.Equal(source, error.Parameters["path"]);
    }

    [Theory]
    [InlineData("../sharedassets0.assets")]
    [InlineData("folder/sharedassets0.assets")]
    [InlineData("folder\\sharedassets0.assets")]
    public void Parse_WhenTargetContainsDirectory_ReturnsFailure(string target)
    {
        string targetJson = JsonSerializer.Serialize(target);
        OperationError error = ParseFailure(CreateManifest($$"""
                                                             {
                                                               "targets": [
                                                                 {
                                                                   "file": {{targetJson}},
                                                                   "patches": [
                                                                     {
                                                                       "type": "Camera",
                                                                       "match": { "m_Name": "Main" }
                                                                     }
                                                                   ]
                                                                 }
                                                               ]
                                                             }
                                                             """));

        Assert.Equal(ManifestErrorCodes.InvalidPath, error.Code);
        Assert.Equal(target, error.Parameters["path"]);
    }

    [Fact]
    public void Parse_WhenLegacyComponentPropertyIsPresent_IgnoresUnknownProperty()
    {
        ModManifest manifest = ParseSuccess(ManifestWithPatchProperty(
            """
            "component": "Transform"
            """));

        Assert.Null(Assert.Single(manifest.Patches).ComponentTypeName);
    }

    [Theory]
    [InlineData("Camera", "\"Transform\"")]
    [InlineData("GameObject", "\"\"")]
    public void Parse_WhenComponentTypeIsInvalid_ReturnsFailure(string assetType, string componentType)
    {
        string json = CreateManifest($$"""
                                       {
                                         "targets": [
                                           {
                                             "file": "sharedassets0.assets",
                                             "patches": [
                                               {
                                                 "type": "{{assetType}}",
                                                 "match": { "m_Name": "Main" },
                                                 "componentType": {{componentType}}
                                               }
                                             ]
                                           }
                                         ]
                                       }
                                       """);

        OperationError error = ParseFailure(json);

        Assert.Equal(ManifestErrorCodes.InvalidValue, error.Code);
        Assert.Equal("componentType", error.Parameters["property"]);
    }

    [Fact]
    public void Parse_WhenComponentTypeAndReplacementAreCombined_ReturnsFailure()
    {
        OperationError error = ParseFailure(CreateManifest(
            """
            {
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    {
                      "type": "GameObject",
                      "match": { "m_Name": "Main" },
                      "componentType": "Transform",
                      "replaceAsset": { "fromFile": "source.assets", "matchField": "m_Name" }
                    }
                  ]
                }
              ]
            }
            """));

        Assert.Equal(ManifestErrorCodes.InvalidValue, error.Code);
        Assert.Equal("replaceAsset", error.Parameters["conflicts_with"]);
    }

    [Fact]
    public void Parse_WhenFieldPathIsEmpty_ReturnsFailure()
    {
        OperationError error = ParseFailure(CreateManifest(
            """
            {
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [ { "type": "Camera", "match": { "": "Main" } } ]
                }
              ]
            }
            """));

        Assert.Equal(ManifestErrorCodes.InvalidPath, error.Code);
        Assert.Equal("non_empty_field_path", error.Parameters["expected"]);
    }

    [Fact]
    public void Parse_WhenValuesContainObjectsAndArrays_PreservesDetachedJsonValues()
    {
        ModManifest manifest = ParseSuccess(ManifestWithPatchProperty(
            """
            "set": {
              "m_Color": {
                "from": { "r": 1.0, "g": 0.5 },
                "to": [1, true, "value"]
              }
            }
            """));

        ModSetOperation operation = Assert.Single(Assert.Single(manifest.Patches).SetOperations);

        Assert.Equal(1.0, operation.From.GetProperty("r").GetDouble());
        Assert.Equal("value", operation.To[2].GetString());
    }

    [Fact]
    public void Parse_WhenOptionalGroupsAreValid_ReturnsGroupsAndContent()
    {
        ModManifest manifest = ParseSuccess(CreateManifest(
            """
            {
              "optional": [
                {
                  "name": "Patch group",
                  "targets": [
                    {
                      "file": "sharedassets1.assets",
                      "patches": [ { "type": "Camera", "match": { "m_Name": "Other" } } ]
                    }
                  ]
                },
                {
                  "name": "Payload group",
                  "copyFiles": [ { "source": "extra/payload.resource" } ]
                }
              ]
            }
            """));

        Assert.Equal(2, manifest.OptionalGroups.Count);
        Assert.Single(manifest.OptionalGroups[0].Patches);
        Assert.Single(manifest.OptionalGroups[1].Files);
    }

    [Theory]
    [InlineData("[{ \"name\": \"Empty\" }]")]
    [InlineData("[{ \"copyFiles\": [{ \"source\": \"a.resource\" }] }]")]
    public void Parse_WhenOptionalGroupIsIncomplete_ReturnsFailure(string optional)
    {
        OperationError error = ParseFailure(CreateManifest($$"""
                                                             {
                                                               "optional": {{optional}}
                                                             }
                                                             """));

        Assert.True(error.Code == ManifestErrorCodes.InvalidValue ||
                    error.Code == ManifestErrorCodes.MissingProperty);
    }

    [Fact]
    public void Parse_WhenOptionalGroupNamesCollideIgnoringCase_ReturnsFailure()
    {
        OperationError error = ParseFailure(CreateManifest(
            """
            {
              "optional": [
                { "name": "Bonus", "copyFiles": [ { "source": "a.resource" } ] },
                { "name": "BONUS", "copyFiles": [ { "source": "b.resource" } ] }
              ]
            }
            """));

        Assert.Equal(ManifestErrorCodes.DuplicateOptionalGroup, error.Code);
        Assert.Equal("BONUS", error.Parameters["name"]);
    }

    [Fact]
    public void Parse_WhenPatchHasNoMutationOperation_PreservesLegacyAcceptance()
    {
        ModManifest manifest = ParseSuccess(CreateManifest("{}"));
        ModPatch patch = Assert.Single(manifest.Patches);

        Assert.Empty(patch.SetOperations);
        Assert.Empty(patch.AddOperations);
        Assert.Null(patch.ReplaceAsset);
        Assert.Null(patch.CopyAsset);
    }

    [Fact]
    public void Parse_WhenUnknownPropertiesArePresent_PreservesLegacyAcceptance()
    {
        ModManifest manifest = ParseSuccess(CreateManifest(
            """
            {
              "unknownTopLevel": true,
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "unknownTarget": true,
                  "patches": [
                    {
                      "type": "Camera",
                      "match": { "m_Name": "Main" },
                      "unknownPatch": true
                    }
                  ]
                }
              ]
            }
            """));

        Assert.Single(manifest.Patches);
    }

    private static string ManifestWithPatchProperty(string property)
    {
        return CreateManifest($$"""
                                {
                                  "targets": [
                                    {
                                      "file": "sharedassets0.assets",
                                      "patches": [
                                        {
                                          "type": "Camera",
                                          "match": { "m_Name": "Main" },
                                          {{property}}
                                        }
                                      ]
                                    }
                                  ]
                                }
                                """);
    }

    private static string CreateManifest(string fragment)
    {
        JsonObject overrides = JsonNode.Parse(fragment)?.AsObject() ??
                               throw new InvalidOperationException("Manifest fragment must be an object.");
        var manifest = new JsonObject
        {
            ["$schema"] = SchemaUri,
            ["name"] = "Test Mod",
            ["author"] = "Test Author",
            ["version"] = "1.0.0",
            ["targets"] = new JsonArray
            {
                new JsonObject
                {
                    ["file"] = "sharedassets0.assets",
                    ["patches"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "Camera",
                            ["match"] = new JsonObject
                            {
                                ["m_Name"] = "Main",
                            },
                        },
                    },
                },
            },
        };

        foreach ((string propertyName, JsonNode? value) in overrides)
        {
            manifest[propertyName] = value?.DeepClone();
        }

        return manifest.ToJsonString();
    }

    private static ModManifest ParseSuccess(string json)
    {
        OperationResult<ModManifest> result = ModManifestParser.Parse(json);
        var success = Assert.IsType<OperationSucceeded<ModManifest>>(result);

        return success.Value;
    }

    private static OperationError ParseFailure(string json)
    {
        OperationResult<ModManifest> result = ModManifestParser.Parse(json);
        var failure = Assert.IsType<OperationFailed<ModManifest>>(result);

        return failure.Error;
    }
}
