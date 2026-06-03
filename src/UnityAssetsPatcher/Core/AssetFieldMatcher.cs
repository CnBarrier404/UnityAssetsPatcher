using System.Globalization;
using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public static class AssetFieldMatcher
{
    public static bool MatchesIncludeGroup(AssetsFieldInfo fieldTree,
        IReadOnlyDictionary<string, JsonElement> includeGroup)
    {
        foreach ((string path, JsonElement expectedValue) in includeGroup)
        {
            AssetsFieldInfo? field = FindField(fieldTree, path);

            if (field is null || !MatchesFieldValue(field, expectedValue))
            {
                return false;
            }
        }

        return true;
    }

    public static AssetsFieldInfo? FindField(AssetsFieldInfo fieldTree, string path)
    {
        var segments = AssetFieldPath.Parse(path);

        if (segments is [{ HasSelector: false }])
        {
            return FindDescendantByName(fieldTree, segments[0].Name);
        }

        AssetsFieldInfo? current = fieldTree;

        foreach (AssetFieldPathSegment segment in segments)
        {
            current = FindChildBySegment(current, segment);

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    public static bool MatchesFieldValue(AssetsFieldInfo field, JsonElement expectedValue)
    {
        if (TryGetObjectValue(expectedValue, out JsonElement objectValue))
        {
            return MatchesObjectValue(field, objectValue);
        }

        return field.Value is not null && MatchesValue(field.Value, expectedValue);
    }

    private static bool MatchesValue(string actualValue, JsonElement expectedValue)
    {
        return expectedValue.ValueKind switch
        {
            JsonValueKind.Number => MatchesNumber(actualValue, expectedValue),
            JsonValueKind.True => MatchesBoolean(actualValue, true),
            JsonValueKind.False => MatchesBoolean(actualValue, false),
            JsonValueKind.String => string.Equals(actualValue, expectedValue.GetString(), StringComparison.Ordinal),
            _ => false,
        };
    }

    public static string FormatJsonValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
    }

    public static bool TryGetObjectValue(JsonElement value, out JsonElement objectValue)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                objectValue = value;
                return true;
            case JsonValueKind.Array when value.GetArrayLength() == 1:
            {
                JsonElement firstElement = value.EnumerateArray().Single();

                if (firstElement.ValueKind == JsonValueKind.Object)
                {
                    objectValue = firstElement;
                    return true;
                }

                break;
            }
        }

        objectValue = default;
        return false;
    }

    private static AssetsFieldInfo? FindDescendantByName(AssetsFieldInfo field, string name)
    {
        if (string.Equals(field.Name, name, StringComparison.Ordinal))
        {
            return field;
        }

        return field.Children
            .Select(child => FindDescendantByName(child, name))
            .OfType<AssetsFieldInfo>()
            .FirstOrDefault();
    }

    private static bool MatchesObjectValue(AssetsFieldInfo field, JsonElement expectedObject)
    {
        foreach (JsonProperty property in expectedObject.EnumerateObject())
        {
            AssetsFieldInfo? child = field.Children.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, property.Name, StringComparison.Ordinal));

            if (child is null || !MatchesFieldValue(child, property.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static AssetsFieldInfo? FindChildBySegment(AssetsFieldInfo field, AssetFieldPathSegment segment)
    {
        return field.Children.FirstOrDefault(child =>
            string.Equals(child.Name, segment.Name, StringComparison.Ordinal) &&
            MatchesSelector(child, segment));
    }

    private static bool MatchesSelector(AssetsFieldInfo field, AssetFieldPathSegment segment)
    {
        if (!segment.HasSelector)
        {
            return true;
        }

        AssetsFieldInfo? selectorField = field.Children.FirstOrDefault(child =>
            string.Equals(child.Name, segment.SelectorFieldName, StringComparison.Ordinal));

        return string.Equals(selectorField?.Value, segment.SelectorValue, StringComparison.Ordinal);
    }

    private static bool MatchesNumber(string actualValue, JsonElement expectedValue)
    {
        return double.TryParse(actualValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double actualNumber)
               && expectedValue.TryGetDouble(out double expectedNumber)
               && Math.Abs(actualNumber - expectedNumber) <= 0.00001d;
    }

    private static bool MatchesBoolean(string actualValue, bool expectedValue)
    {
        if (bool.TryParse(actualValue, out bool actualBoolean))
        {
            return actualBoolean == expectedValue;
        }

        if (long.TryParse(actualValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long actualInteger))
        {
            return actualInteger != 0 == expectedValue;
        }

        return false;
    }
}
