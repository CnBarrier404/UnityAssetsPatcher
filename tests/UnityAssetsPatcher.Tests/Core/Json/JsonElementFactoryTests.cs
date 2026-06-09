using System.Text.Json;
using UnityAssetsPatcher.Core.Json;
using Xunit;

namespace UnityAssetsPatcher.Tests.Core.Json;

public sealed class JsonElementFactoryTests
{
    [Fact]
    public void String_EscapesStringValue()
    {
        JsonElement element = JsonElementFactory.String("Player \"One\"");

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Equal("Player \"One\"", element.GetString());
        Assert.Equal("\"Player \\u0022One\\u0022\"", element.GetRawText());
    }

    [Fact]
    public void Number_CreatesSignedIntegerValue()
    {
        JsonElement element = JsonElementFactory.Number(-12L);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Equal(-12, element.GetInt64());
    }

    [Fact]
    public void Array_CreatesArrayFromExistingElements()
    {
        JsonElement element = JsonElementFactory.Array(
        [
            JsonElementFactory.String("Player"),
            JsonElementFactory.Number(3L),
            JsonElementFactory.Boolean(true),
        ]);

        Assert.Equal(JsonValueKind.Array, element.ValueKind);

        var values = element.EnumerateArray().ToArray();
        Assert.Equal("Player", values[0].GetString());
        Assert.Equal(3, values[1].GetInt64());
        Assert.True(values[2].GetBoolean());
    }
}
