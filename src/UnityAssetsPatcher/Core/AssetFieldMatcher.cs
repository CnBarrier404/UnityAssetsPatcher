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

            if (field?.Value is null || !MatchesValue(field.Value, expectedValue))
            {
                return false;
            }
        }

        return true;
    }

    public static AssetsFieldInfo? FindField(AssetsFieldInfo fieldTree, string path)
    {
        if (!path.Contains('.', StringComparison.Ordinal))
        {
            return FindDescendantByName(fieldTree, path);
        }

        AssetsFieldInfo? current = fieldTree;

        foreach (string segment in path.Split('.'))
        {
            current = current.Children.FirstOrDefault(child =>
                string.Equals(child.Name, segment, StringComparison.Ordinal));

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    public static bool MatchesValue(string actualValue, JsonElement expectedValue)
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
