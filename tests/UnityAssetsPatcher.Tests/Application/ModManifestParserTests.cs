using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ModManifestParserTests
{
    /// <summary>
    /// Verifies that match groups are parsed from a patch element with field-value pairs.
    /// </summary>
    [Fact]
    public void ReadMatchGroups_WhenPatchHasMatch_ReturnsFieldValueMap()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": {
                "field of view": 90.0,
                "m_Name": "Main Camera"
              }
            }
            """);

        var matchGroups = ModManifestParser.ReadMatchGroups(patchElement);

        Assert.Single(matchGroups);
        var match = Assert.Single(matchGroups);
        Assert.Equal(2, match.Count);
        Assert.Equal(90.0, match["field of view"].GetDouble());
        Assert.Equal("Main Camera", match["m_Name"].GetString());
    }

    /// <summary>
    /// Verifies that match object cannot be empty.
    /// </summary>
    [Fact]
    public void ReadMatchGroups_WhenMatchIsEmpty_ThrowsInvalidOperationException()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": {}
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => ModManifestParser.ReadMatchGroups(patchElement));

        Assert.Contains("cannot be empty", exception.Message);
    }

    /// <summary>
    /// Verifies that set operations are parsed with field path, from, and to values.
    /// </summary>
    [Fact]
    public void ReadSetOperations_WhenPatchHasSet_ReturnsOperations()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": {
                "field of view": {
                  "from": 90.0,
                  "to": 75.0
                }
              }
            }
            """);

        var operations = ModManifestParser.ReadSetOperations(patchElement);

        ManifestSetOperation operation = Assert.Single(operations!);
        Assert.Equal("field of view", operation.FieldPath);
        Assert.Equal(90.0, operation.From.GetDouble());
        Assert.Equal(75.0, operation.To.GetDouble());
    }

    /// <summary>
    /// Verifies that add operations are parsed with field path and array values.
    /// </summary>
    [Fact]
    public void ReadAddOperations_WhenPatchHasAdd_ReturnsOperations()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "add": {
                "m_ValidKeywords.Array": ["_EMISSION", "_SPECULAR"]
              }
            }
            """);

        var operations = ModManifestParser.ReadAddOperations(patchElement);

        ManifestAddOperation operation = Assert.Single(operations!);
        Assert.Equal("m_ValidKeywords.Array", operation.FieldPath);
        Assert.Equal(2, operation.Value.GetArrayLength());
        Assert.Equal("_EMISSION", operation.Value[0].GetString());
        Assert.Equal("_SPECULAR", operation.Value[1].GetString());
    }

    /// <summary>
    /// Verifies that set returns null when the patch has no set property.
    /// </summary>
    [Fact]
    public void ReadSetOperations_WhenPatchHasNoSet_ReturnsNull()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 }
            }
            """);

        Assert.Null(ModManifestParser.ReadSetOperations(patchElement));
    }

    /// <summary>
    /// Verifies that add returns null when the patch has no add property.
    /// </summary>
    [Fact]
    public void ReadAddOperations_WhenPatchHasNoAdd_ReturnsNull()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 }
            }
            """);

        Assert.Null(ModManifestParser.ReadAddOperations(patchElement));
    }

    /// <summary>
    /// Verifies that set field value must be an object with from and to properties.
    /// </summary>
    [Fact]
    public void ReadSetOperations_WhenSetValueIsNotObject_ThrowsInvalidOperationException()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": {
                "field of view": "not an object"
              }
            }
            """);

        Assert.Throws<InvalidOperationException>(() => ModManifestParser.ReadSetOperations(patchElement));
    }

    /// <summary>
    /// Verifies that set field value must contain a from property.
    /// </summary>
    [Fact]
    public void ReadSetOperations_WhenSetMissingFrom_ThrowsInvalidOperationException()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": {
                "field of view": { "to": 75.0 }
              }
            }
            """);

        var exception =
            Assert.Throws<InvalidOperationException>(() => ModManifestParser.ReadSetOperations(patchElement));

        Assert.Contains("from", exception.Message);
    }

    /// <summary>
    /// Verifies that set field value must contain a to property.
    /// </summary>
    [Fact]
    public void ReadSetOperations_WhenSetMissingTo_ThrowsInvalidOperationException()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": {
                "field of view": { "from": 90.0 }
              }
            }
            """);

        var exception =
            Assert.Throws<InvalidOperationException>(() => ModManifestParser.ReadSetOperations(patchElement));

        Assert.Contains("to", exception.Message);
    }

    /// <summary>
    /// Verifies that add field value must be an array.
    /// </summary>
    [Fact]
    public void ReadAddOperations_WhenAddValueIsNotArray_ThrowsInvalidOperationException()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "add": {
                "m_ValidKeywords.Array": "not an array"
              }
            }
            """);

        Assert.Throws<InvalidOperationException>(() => ModManifestParser.ReadAddOperations(patchElement));
    }

    /// <summary>
    /// Verifies that multiple set operations are parsed from a single patch.
    /// </summary>
    [Fact]
    public void ReadSetOperations_WhenPatchHasMultipleSetFields_ReturnsAllOperations()
    {
        JsonElement patchElement = ParsePatchElement(
            """
            {
              "type": "Camera",
              "match": { "field of view": 90.0 },
              "set": {
                "field of view": { "from": 90.0, "to": 75.0 },
                "m_IsActive": { "from": false, "to": true }
              }
            }
            """);

        var operations = ModManifestParser.ReadSetOperations(patchElement);

        Assert.Equal(2, operations!.Length);
        Assert.Equal("field of view", operations[0].FieldPath);
        Assert.Equal("m_IsActive", operations[1].FieldPath);
    }

    private static JsonElement ParsePatchElement(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
