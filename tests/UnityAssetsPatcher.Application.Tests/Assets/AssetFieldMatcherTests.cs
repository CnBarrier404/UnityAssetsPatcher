using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Assets;

public sealed class AssetFieldMatcherTests
{
    [Theory]
    [MemberData(nameof(NumericScalarValues))]
    public void MatchesValue_WhenScalarHasOriginalIntegerWidth_MatchesJsonInteger(
        AssetScalarValue actual,
        string expectedJson)
    {
        using JsonDocument document = JsonDocument.Parse(expectedJson);
        AssetField field = new AssetScalarField("value", actual.Kind.ToString(), actual);

        bool result = AssetFieldMatcher.MatchesValue(field, document.RootElement);

        Assert.True(result);
    }

    public static IEnumerable<object[]> NumericScalarValues()
    {
        yield return new object[] { new AssetScalarValue.Int8(-8), "-8" };
        yield return new object[] { new AssetScalarValue.UInt8(8), "8" };
        yield return new object[] { new AssetScalarValue.Int16(-16), "-16" };
        yield return new object[] { new AssetScalarValue.UInt16(16), "16" };
        yield return new object[] { new AssetScalarValue.Int32(-32), "-32" };
        yield return new object[] { new AssetScalarValue.UInt32(32), "32" };
    }
}
