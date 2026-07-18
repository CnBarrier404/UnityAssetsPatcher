using System.Globalization;
using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Assets;

public sealed class AssetFieldMatcherTests
{
    [Theory]
    [InlineData("9007199254740992", true)]
    [InlineData("9007199254740993", false)]
    public void MatchesFieldValue_WhenSignedIntegersDifferBeyondDoublePrecision_ComparesExactly(
        string expectedJson,
        bool expectedMatch)
    {
        var field = new AssetsFieldInfo(
            "value", "SInt64", new Int64AssetFieldValue(9007199254740992), []);

        Assert.Equal(expectedMatch, AssetFieldMatcher.MatchesFieldValue(field, ParseJson(expectedJson)));
    }

    [Theory]
    [InlineData("18446744073709551615", true)]
    [InlineData("18446744073709551614", false)]
    public void MatchesFieldValue_WhenUnsignedIntegerIsNearUInt64Limit_ComparesExactly(
        string expectedJson,
        bool expectedMatch)
    {
        var field = new AssetsFieldInfo(
            "value", "UInt64", new UInt64AssetFieldValue(ulong.MaxValue), []);

        Assert.Equal(expectedMatch, AssetFieldMatcher.MatchesFieldValue(field, ParseJson(expectedJson)));
    }

    [Fact]
    public void MatchesFieldValue_WhenFloatingPointTypesDiffer_UsesStoredUnityType()
    {
        JsonElement expected = ParseJson("0.10000000149011612");
        var singleField = new AssetsFieldInfo(
            "single", "float", new FloatAssetFieldValue(0.1f), []);
        var doubleField = new AssetsFieldInfo(
            "double", "double", new DoubleAssetFieldValue(0.1d), []);

        Assert.True(AssetFieldMatcher.MatchesFieldValue(singleField, expected));
        Assert.False(AssetFieldMatcher.MatchesFieldValue(doubleField, expected));
    }

    [Fact]
    public void TypedValues_WhenCurrentCultureUsesDecimalComma_RemainInvariant()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var field = new AssetsFieldInfo(
                "value", "double", new DoubleAssetFieldValue(12.5d), []);

            Assert.Equal("12.5", field.Value?.ToInvariantString());
            Assert.True(AssetFieldMatcher.MatchesFieldValue(field, ParseJson("12.5")));
            Assert.False(AssetFieldMatcher.MatchesFieldValue(field, ParseJson("125")));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
