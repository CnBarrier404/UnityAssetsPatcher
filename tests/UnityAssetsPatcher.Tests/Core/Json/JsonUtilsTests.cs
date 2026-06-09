using System.Text.Json;
using UnityAssetsPatcher.Core.Json;
using Xunit;

namespace UnityAssetsPatcher.Tests.Core.Json;

public sealed class JsonUtilsTests
{
    [Fact]
    public void ParseElement_ReturnsJsonElementUsableAfterDocumentDisposal()
    {
        const string json = """
                            {
                              "name": "Player",
                              "retryCount": 3
                            }
                            """;

        JsonElement element = JsonUtils.ParseElement(json);

        Assert.Equal("Player", element.GetProperty("name").GetString());
        Assert.Equal(3, element.GetProperty("retryCount").GetInt32());
    }

    [Fact]
    public void FormatElementValue_WhenValueIsString_ReturnsUnquotedString()
    {
        JsonElement element = JsonUtils.ParseElement("\"Player\"");

        string result = JsonUtils.FormatElementValue(element);

        Assert.Equal("Player", result);
    }

    [Theory]
    [InlineData("3", "3")]
    [InlineData("true", "true")]
    [InlineData("{\"name\":\"Player\"}", "{\"name\":\"Player\"}")]
    [InlineData("[1,2]", "[1,2]")]
    public void FormatElementValue_WhenValueIsNotString_ReturnsRawJson(string json, string expected)
    {
        JsonElement element = JsonUtils.ParseElement(json);

        string result = JsonUtils.FormatElementValue(element);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{\"name\":\"Player\"}")]
    [InlineData("[{\"name\":\"Player\"}]")]
    public void TryGetObjectValue_WhenValueIsObjectOrSingleObjectArray_ReturnsObject(string json)
    {
        JsonElement element = JsonUtils.ParseElement(json);

        bool result = JsonUtils.TryGetObjectValue(element, out JsonElement objectValue);

        Assert.True(result);
        Assert.Equal(JsonValueKind.Object, objectValue.ValueKind);
        Assert.Equal("Player", objectValue.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"Player\"]")]
    [InlineData("[{\"name\":\"Player\"},{\"name\":\"Camera\"}]")]
    [InlineData("\"Player\"")]
    public void TryGetObjectValue_WhenValueIsNotObjectShape_ReturnsFalse(string json)
    {
        JsonElement element = JsonUtils.ParseElement(json);

        bool result = JsonUtils.TryGetObjectValue(element, out JsonElement objectValue);

        Assert.False(result);
        Assert.Equal(default, objectValue);
    }

    [Fact]
    public void ReadElementFromFile_ReadsJsonElement()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(
                path,
                """
                {
                  "type": "Camera"
                }
                """);

            JsonElement element = JsonUtils.ReadElementFromFile(path);

            Assert.Equal("Camera", element.GetProperty("type").GetString());
        }
        finally
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReadRequiredProperty_WhenPropertyHasExpectedKind_ReturnsProperty()
    {
        JsonElement element = JsonUtils.ParseElement(
            """
            {
              "items": []
            }
            """);

        JsonElement property = JsonUtils.ReadRequiredProperty(
            element,
            "items",
            JsonValueKind.Array,
            "Sample");

        Assert.Equal(JsonValueKind.Array, property.ValueKind);
    }

    [Fact]
    public void ReadRequiredProperty_WhenPropertyIsMissing_ThrowsClearError()
    {
        JsonElement element = JsonUtils.ParseElement("{}");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonUtils.ReadRequiredProperty(element, "items", JsonValueKind.Array, "Sample"));

        Assert.Equal("Sample must contain an array 'items' property.", exception.Message);
    }

    [Fact]
    public void ReadRequiredProperty_WhenPropertyHasWrongKind_ThrowsClearError()
    {
        JsonElement element = JsonUtils.ParseElement(
            """
            {
              "items": {}
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonUtils.ReadRequiredProperty(element, "items", JsonValueKind.Array, "Sample"));

        Assert.Equal("Sample must contain an array 'items' property.", exception.Message);
    }

    [Fact]
    public void TryReadProperty_WhenPropertyIsMissing_ReturnsFalse()
    {
        JsonElement element = JsonUtils.ParseElement("{}");

        bool found = JsonUtils.TryReadProperty(element, "items", JsonValueKind.Array, out JsonElement property);

        Assert.False(found);
        Assert.Equal(default, property);
    }

    [Fact]
    public void TryReadProperty_WhenPropertyHasWrongKind_ThrowsClearError()
    {
        JsonElement element = JsonUtils.ParseElement(
            """
            {
              "items": {}
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonUtils.TryReadProperty(element, "items", JsonValueKind.Array, out _));

        Assert.Equal("Property 'items' must be an array.", exception.Message);
    }

    [Fact]
    public void ReadRequiredStringProperty_WhenPropertyIsString_ReturnsValue()
    {
        JsonElement element = JsonUtils.ParseElement(
            """
            {
              "name": "Player"
            }
            """);

        string value = JsonUtils.ReadRequiredStringProperty(element, "name", "Sample");

        Assert.Equal("Player", value);
    }

    [Fact]
    public void ReadOptionalStringProperty_WhenPropertyIsMissing_ReturnsNull()
    {
        JsonElement element = JsonUtils.ParseElement("{}");

        string? value = JsonUtils.ReadOptionalStringProperty(element, "description", "Sample");

        Assert.Null(value);
    }

    [Fact]
    public void ReadOptionalStringProperty_WhenPropertyHasWrongKind_ThrowsClearError()
    {
        JsonElement element = JsonUtils.ParseElement(
            """
            {
              "description": 42
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonUtils.ReadOptionalStringProperty(element, "description", "Sample"));

        Assert.Equal("Sample 'description' property must be a string.", exception.Message);
    }
}
