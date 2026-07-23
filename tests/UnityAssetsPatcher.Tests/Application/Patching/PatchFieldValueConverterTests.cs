using System.Text.Json;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Json;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Patching;

public sealed class PatchFieldValueConverterTests
{
    [Fact]
    public void CreateAddArrayValue_WhenValuesExistOrRepeat_AppendsOnlyFirstMissingValues()
    {
        AssetField arrayField = CreateArrayField(
            new AssetFieldValue.String("existing"),
            new AssetFieldValue.Boolean(true),
            new AssetFieldValue.Int64(10));
        JsonElement additions = JsonUtils.ParseElement(
            """["existing","new","new",true,false,false,10,11,11]""");

        JsonElement result = PatchFieldValueConverter.CreateAddArrayValue(arrayField, additions, out bool changed);

        Assert.True(changed);
        Assert.Equal("""["existing",true,10,"new",false,11]""", result.GetRawText());
    }

    [Fact]
    public void CreateAddArrayValue_WhenNumbersUseTolerance_PreservesSequentialDeduplication()
    {
        AssetField arrayField = CreateArrayField();
        JsonElement additions = JsonUtils.ParseElement("[0,0.000009,0.000018]");

        JsonElement result = PatchFieldValueConverter.CreateAddArrayValue(arrayField, additions, out bool changed);

        Assert.True(changed);
        Assert.Equal("[0,0.000018]", result.GetRawText());
    }

    [Fact]
    public void CreateAddArrayValue_WhenCurrentNumericValuesMatch_DoesNotAppend()
    {
        AssetField arrayField = CreateArrayField(
            new AssetFieldValue.Float(1f),
            new AssetFieldValue.Double(2d),
            new AssetFieldValue.UInt64(ulong.MaxValue));
        JsonElement additions = JsonUtils.ParseElement("[1.0000001,2,18446744073709551615]");

        JsonElement result = PatchFieldValueConverter.CreateAddArrayValue(arrayField, additions, out bool changed);

        Assert.False(changed);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void CreateAddArrayValue_WhenCurrentAndAddedArraysAreLarge_CompletesWithAllUniqueValues()
    {
        const int elementCount = 50_000;
        AssetField arrayField = CreateArrayField(Enumerable.Range(0, elementCount)
            .Select(value => (AssetFieldValue)new AssetFieldValue.Int64(value))
            .ToArray());
        JsonElement additions = JsonUtils.ParseElement(
            $"[{string.Join(',', Enumerable.Range(elementCount, elementCount))}]");

        JsonElement result = PatchFieldValueConverter.CreateAddArrayValue(arrayField, additions, out bool changed);

        Assert.True(changed);
        Assert.Equal(elementCount * 2, result.GetArrayLength());
        Assert.Equal(elementCount * 2 - 1, result.EnumerateArray().Last().GetInt32());
    }

    private static AssetField CreateArrayField(params AssetFieldValue[] values)
    {
        return new AssetField(
            "Array",
            "Array",
            null,
            values.Select(value => new AssetField("data", "scalar", value, [])).ToArray());
    }
}
