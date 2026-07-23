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

    private static bool MatchesScalar(AssetFieldValue actualValue, JsonElement expectedValue)
    {
        return actualValue switch
        {
            AssetFieldValue.Boolean value =>
                expectedValue.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                value.Value == expectedValue.GetBoolean(),
            AssetFieldValue.String value =>
                expectedValue.ValueKind == JsonValueKind.String &&
                string.Equals(value.Value, expectedValue.GetString(), StringComparison.Ordinal),
            AssetFieldValue.Int64 value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetInt64(out long expected) && value.Value == expected,
            AssetFieldValue.UInt64 value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetUInt64(out ulong expected) && value.Value == expected,
            AssetFieldValue.Float value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetSingle(out float expected) && FloatValuesEqual(value.Value, expected),
            AssetFieldValue.Double value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetDouble(out double expected) && value.Value.Equals(expected),
            _ => false,
        };
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

        IReadOnlyList<AssetField> children = AssetFieldNavigator.GetArrayElements(arrayField);

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
