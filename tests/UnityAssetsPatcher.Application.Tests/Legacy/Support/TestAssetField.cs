using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Tests;

public static class TestAssetField
{
    public static AssetField Create(
        string name,
        string typeName,
        AssetFieldValue? value,
        IEnumerable<AssetField?> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        return value is null
            ? new AssetObjectField(name, typeName, children)
            : new AssetScalarField(name, typeName, ConvertValue(value));
    }

    private static AssetScalarValue ConvertValue(AssetFieldValue value)
    {
        return value switch
        {
            AssetScalarValue scalar => scalar,
            AssetFieldValue.Boolean boolean => new AssetScalarValue.Boolean(boolean.Value),
            AssetFieldValue.String text => new AssetScalarValue.String(text.Value),
            AssetFieldValue.Int64 integer => new AssetScalarValue.Int64(integer.Value),
            AssetFieldValue.UInt64 integer => new AssetScalarValue.UInt64(integer.Value),
            AssetFieldValue.Float single => new AssetScalarValue.Float(single.Value),
            AssetFieldValue.Double doubleValue => new AssetScalarValue.Double(doubleValue.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported test asset value."),
        };
    }
}
