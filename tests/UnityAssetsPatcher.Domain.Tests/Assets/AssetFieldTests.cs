using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Domain.Tests.Assets;

public sealed class AssetFieldTests
{
    [Fact]
    public void Value_WhenReadThroughBaseField_PreservesAllScalarTypes()
    {
        AssetScalarValue[] values =
        [
            new AssetScalarValue.Boolean(true),
            new AssetScalarValue.Int8(-8),
            new AssetScalarValue.UInt8(8),
            new AssetScalarValue.Int16(-16),
            new AssetScalarValue.UInt16(16),
            new AssetScalarValue.Int32(-32),
            new AssetScalarValue.UInt32(32),
            new AssetScalarValue.Int64(-64),
            new AssetScalarValue.UInt64(64),
            new AssetScalarValue.Float(1.5f),
            new AssetScalarValue.Double(2.5d),
            new AssetScalarValue.String("text")
        ];

        foreach (AssetScalarValue expected in values)
        {
            AssetField field = new AssetScalarField("value", expected.Kind.ToString(), expected);

            Assert.Same(expected, field.Value);
        }
    }
}
