using System.Text.Json;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Json;

namespace UnityAssetsPatcher.Application.Assets;

public static class AssetFieldMatcher
{
    private const int FloatComparisonMaxUlps = 16;

    public static bool MatchesFields(AssetField fieldTree, IReadOnlyDictionary<string, JsonElement> expectedFields)
    {
        foreach ((string path, JsonElement expectedValue) in expectedFields)
        {
            AssetField? field = AssetFieldNavigator.Find(fieldTree, path);

            if (field is null || !MatchesValue(field, expectedValue))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesValue(AssetField field, JsonElement expectedValue)
    {
        if (JsonUtils.TryGetObjectValue(expectedValue, out JsonElement objectValue))
        {
            return MatchesObject(field, objectValue);
        }

        if (expectedValue.ValueKind == JsonValueKind.Array)
        {
            return MatchesArray(field, expectedValue);
        }

        return field.Value is not null && MatchesScalar(field.Value, expectedValue);
    }

    private static bool MatchesScalar(AssetScalarValue actualValue, JsonElement expectedValue)
    {
        return actualValue switch
        {
            AssetScalarValue.Boolean value =>
                expectedValue.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                value.Value == expectedValue.GetBoolean(),
            AssetScalarValue.String value =>
                expectedValue.ValueKind == JsonValueKind.String &&
                string.Equals(value.Value, expectedValue.GetString(), StringComparison.Ordinal),
            AssetScalarValue.Int8 value => MatchesSignedInteger(value.Value, expectedValue),
            AssetScalarValue.Int16 value => MatchesSignedInteger(value.Value, expectedValue),
            AssetScalarValue.Int32 value => MatchesSignedInteger(value.Value, expectedValue),
            AssetScalarValue.Int64 value => MatchesSignedInteger(value.Value, expectedValue),
            AssetScalarValue.UInt8 value => MatchesUnsignedInteger(value.Value, expectedValue),
            AssetScalarValue.UInt16 value => MatchesUnsignedInteger(value.Value, expectedValue),
            AssetScalarValue.UInt32 value => MatchesUnsignedInteger(value.Value, expectedValue),
            AssetScalarValue.UInt64 value => MatchesUnsignedInteger(value.Value, expectedValue),
            AssetScalarValue.Float value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetSingle(out float expected) && FloatValuesEqual(value.Value, expected),
            AssetScalarValue.Double value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetDouble(out double expected) && value.Value.Equals(expected),
            _ => false,
        };
    }

    private static bool MatchesSignedInteger(long actualValue, JsonElement expectedValue)
    {
        return expectedValue.ValueKind == JsonValueKind.Number &&
               expectedValue.TryGetInt64(out long expected) && actualValue == expected;
    }

    private static bool MatchesUnsignedInteger(ulong actualValue, JsonElement expectedValue)
    {
        return expectedValue.ValueKind == JsonValueKind.Number &&
               expectedValue.TryGetUInt64(out ulong expected) && actualValue == expected;
    }

    private static bool FloatValuesEqual(float actual, float expected)
    {
        if (actual.Equals(expected))
        {
            return true;
        }

        if (!float.IsFinite(actual) || !float.IsFinite(expected) || MathF.Sign(actual) != MathF.Sign(expected))
        {
            return false;
        }

        int actualBits = BitConverter.SingleToInt32Bits(actual);
        int expectedBits = BitConverter.SingleToInt32Bits(expected);

        return Math.Abs((long)actualBits - expectedBits) <= FloatComparisonMaxUlps;
    }

    private static bool MatchesObject(AssetField field, JsonElement expectedObject)
    {
        return expectedObject
            .EnumerateObject()
            .All(property => MatchesObjectProperty(field, property));
    }

    private static bool MatchesObjectProperty(AssetField field, JsonProperty property)
    {
        AssetField? child = field.FindChild(property.Name);

        return child is not null && MatchesValue(child, property.Value);
    }

    private static bool MatchesArray(AssetField field, JsonElement expectedArray)
    {
        AssetField? arrayField = AssetFieldNavigator.ResolveArray(field);

        if (arrayField is null)
        {
            return false;
        }

        var children = AssetFieldNavigator.GetArrayElements(arrayField);

        if (children.Count != expectedArray.GetArrayLength())
        {
            return false;
        }

        int index = 0;

        foreach (JsonElement expectedElement in expectedArray.EnumerateArray())
        {
            if (!MatchesValue(children[index], expectedElement))
            {
                return false;
            }

            index++;
        }

        return true;
    }
}
