using System.Text.Json;
using UnityAssetsPatcher.Application.Json;

namespace UnityAssetsPatcher.Application.Assets;

public static class AssetFieldMatcher
{
    private const int FloatComparisonMaxUlps = 16;

    public static bool MatchesFields(AssetsFieldInfo fieldTree, IReadOnlyDictionary<string, JsonElement> expectedFields)
    {
        foreach ((string path, JsonElement expectedValue) in expectedFields)
        {
            AssetsFieldInfo? field = AssetFieldNavigator.FindField(fieldTree, path);

            if (field is null || !MatchesFieldValue(field, expectedValue))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesFieldValue(AssetsFieldInfo field, JsonElement expectedValue)
    {
        if (JsonUtils.TryGetObjectValue(expectedValue, out JsonElement objectValue))
        {
            return MatchesObjectValue(field, objectValue);
        }

        if (expectedValue.ValueKind == JsonValueKind.Array)
        {
            return MatchesArrayValue(field, expectedValue);
        }

        return field.Value is not null && MatchesValue(field.Value, expectedValue);
    }

    public static bool MatchesValue(AssetFieldValue actualValue, JsonElement expectedValue)
    {
        return actualValue switch
        {
            BoolAssetFieldValue value =>
                expectedValue.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                value.Value == expectedValue.GetBoolean(),
            StringAssetFieldValue value =>
                expectedValue.ValueKind == JsonValueKind.String &&
                string.Equals(value.Value, expectedValue.GetString(), StringComparison.Ordinal),
            Int64AssetFieldValue value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetInt64(out long expected) && value.Value == expected,
            UInt64AssetFieldValue value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetUInt64(out ulong expected) && value.Value == expected,
            FloatAssetFieldValue value =>
                expectedValue.ValueKind == JsonValueKind.Number &&
                expectedValue.TryGetSingle(out float expected) && FloatValuesEqual(value.Value, expected),
            DoubleAssetFieldValue value =>
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

    private static bool MatchesObjectValue(AssetsFieldInfo field, JsonElement expectedObject)
    {
        return expectedObject
            .EnumerateObject()
            .All(property => MatchesObjectProperty(field, property));
    }

    private static bool MatchesObjectProperty(AssetsFieldInfo field, JsonProperty property)
    {
        AssetsFieldInfo? child = field.Child(property.Name);

        return child is not null && MatchesFieldValue(child, property.Value);
    }

    private static bool MatchesArrayValue(AssetsFieldInfo field, JsonElement expectedArray)
    {
        AssetsFieldInfo? arrayField = AssetFieldNavigator.ResolveArrayField(field);

        if (arrayField is null)
        {
            return false;
        }

        var children = AssetFieldNavigator.GetArrayElementFields(arrayField);

        if (children.Count != expectedArray.GetArrayLength())
        {
            return false;
        }

        int index = 0;

        foreach (JsonElement expectedElement in expectedArray.EnumerateArray())
        {
            if (!MatchesFieldValue(children[index], expectedElement))
            {
                return false;
            }

            index++;
        }

        return true;
    }
}
