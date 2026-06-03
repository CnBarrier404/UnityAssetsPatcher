using System.Text.Json;
using UnityAssetsPatcher.Utils;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class JsonUtilsTests
{
    [Fact]
    public void Serialize_UsesCamelCaseIndentedJson()
    {
        var value = new SampleConfig("Player", 3);

        string json = JsonUtils.Serialize(value);

        Assert.Contains("\"name\": \"Player\"", json, StringComparison.Ordinal);
        Assert.Contains("\"retryCount\": 3", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Name\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_ReadsJsonIntoRequestedType()
    {
        const string json = """
                            {
                              "name": "Player",
                              "retryCount": 3
                            }
                            """;

        var value = JsonUtils.Deserialize<SampleConfig>(json);

        Assert.Equal("Player", value.Name);
        Assert.Equal(3, value.RetryCount);
    }

    [Fact]
    public void WriteAndReadFile_RoundTripsJson()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        var value = new SampleConfig("Enemy", 5);

        try
        {
            JsonUtils.WriteToFile(path, value);

            var result = JsonUtils.ReadFromFile<SampleConfig>(path);

            Assert.Equal(value, result);
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

    private sealed record SampleConfig(string Name, int RetryCount);
}
