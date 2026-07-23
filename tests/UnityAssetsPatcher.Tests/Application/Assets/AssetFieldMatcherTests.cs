using System.Globalization;
using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Assets;

public sealed class AssetFieldMatcherTests
{
    [Theory]
    [InlineData("9007199254740992", true)]
    [InlineData("9007199254740993", false)]
    public void MatchesValue_WhenSignedIntegersDifferBeyondDoublePrecision_ComparesExactly(
        string expectedJson,
        bool expectedMatch)
    {
        var field = new AssetField(
            "value", "SInt64", new AssetFieldValue.Int64(9007199254740992), []);

        Assert.Equal(expectedMatch, AssetFieldMatcher.MatchesValue(field, ParseJson(expectedJson)));
    }

    [Theory]
    [InlineData("18446744073709551615", true)]
    [InlineData("18446744073709551614", false)]
    public void MatchesValue_WhenUnsignedIntegerIsNearUInt64Limit_ComparesExactly(
        string expectedJson,
        bool expectedMatch)
    {
        var field = new AssetField(
            "value", "UInt64", new AssetFieldValue.UInt64(ulong.MaxValue), []);

        Assert.Equal(expectedMatch, AssetFieldMatcher.MatchesValue(field, ParseJson(expectedJson)));
    }

    [Fact]
    public void MatchesValue_WhenFloatingPointTypesDiffer_UsesStoredUnityType()
    {
        JsonElement expected = ParseJson("0.10000000149011612");
        var singleField = new AssetField(
            "single", "float", new AssetFieldValue.Float(0.1f), []);
        var doubleField = new AssetField(
            "double", "double", new AssetFieldValue.Double(0.1d), []);

        Assert.True(AssetFieldMatcher.MatchesValue(singleField, expected));
        Assert.False(AssetFieldMatcher.MatchesValue(doubleField, expected));
    }

    [Theory]
    [InlineData(1.2794967f, "1.2794979", true)]
    [InlineData(0.10342161f, "0.1034217", true)]
    [InlineData(1.2794f, "1.2794979", false)]
    public void MatchesValue_WhenSingleValuesHaveMinorDrift_AllowsLimitedUlpDifference(
        float actual,
        string expectedJson,
        bool expectedMatch)
    {
        var field = new AssetField(
            "value", "float", new AssetFieldValue.Float(actual), []);

        Assert.Equal(expectedMatch, AssetFieldMatcher.MatchesValue(field, ParseJson(expectedJson)));
    }

    [Fact]
    public void TypedValues_WhenCurrentCultureUsesDecimalComma_RemainInvariant()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var field = new AssetField(
                "value", "double", new AssetFieldValue.Double(12.5d), []);

            Assert.Equal("12.5", field.Value?.ToInvariantString());
            Assert.True(AssetFieldMatcher.MatchesValue(field, ParseJson("12.5")));
            Assert.False(AssetFieldMatcher.MatchesValue(field, ParseJson("125")));
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
