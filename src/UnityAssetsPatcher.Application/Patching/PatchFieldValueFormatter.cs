using System.Globalization;
using System.Text.Json;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Patching;

public static class PatchFieldValueFormatter
{
    public static AssetsFieldInfo? FindDirectChild(AssetsFieldInfo field, string name)
    {
        return field.Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.Ordinal));
    }

    public static bool IsJsonArrayPatchValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Array &&
               !AssetFieldMatcher.TryGetObjectValue(value, out _);
    }

    public static AssetsFieldInfo? ResolveArrayField(AssetsFieldInfo? field)
    {
        if (field is null)
        {
            return null;
        }

        if (IsArrayField(field))
        {
            return field;
        }

        return FindDirectChild(field, "Array");
    }

    public static string ResolveArrayFieldPath(
        string fieldPath,
        AssetsFieldInfo? field,
        AssetsFieldInfo? arrayField)
    {
        return field is not null && arrayField is not null && !ReferenceEquals(field, arrayField)
            ? $"{fieldPath}.{arrayField.Name}"
            : fieldPath;
    }

    public static IReadOnlyList<AssetsFieldInfo> GetArrayElementFields(AssetsFieldInfo arrayField)
    {
        var dataChildren = arrayField.Children
            .Where(child => string.Equals(child.Name, "data", StringComparison.Ordinal))
            .ToArray();

        return dataChildren.Length > 0 ? dataChildren : arrayField.Children;
    }

    public static JsonElement GetObjectPropertyOrDefault(JsonElement value, string propertyName)
    {
        return AssetFieldMatcher.TryGetObjectValue(value, out JsonElement objectValue) &&
               objectValue.TryGetProperty(propertyName, out JsonElement propertyValue)
            ? propertyValue.Clone()
            : value;
    }

    public static string FormatObjectFieldValue(AssetsFieldInfo field)
    {
        string properties = string.Join(", ", field.Children
            .Where(child => child.Value is not null)
            .Select(child => $"{child.Name}: {child.Value}"));

        return properties.Length == 0 ? "<missing>" : $"{{ {properties} }}";
    }

    public static string FormatArrayFieldValue(AssetsFieldInfo arrayField)
    {
        string elements = string.Join(", ", GetArrayElementFields(arrayField).Select(FormatArrayElementValue));

        return $"[{elements}]";
    }

    public static JsonElement CreateAddArrayValue(
        AssetsFieldInfo arrayField,
        JsonElement value,
        out bool changed)
    {
        var currentFields = GetArrayElementFields(arrayField);
        var elements = currentFields
            .Select(CreateJsonElementFromArrayElementField)
            .ToList();
        changed = false;

        foreach (JsonElement element in value.EnumerateArray())
        {
            if (ContainsArrayValue(currentFields, elements, element))
            {
                continue;
            }

            elements.Add(element.Clone());
            changed = true;
        }

        return JsonElementFactory.Array(elements);
    }

    public static void EnsureSupportedPatchValue(JsonElement value, string path)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number
            or JsonValueKind.String)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Patch operation for field '{path}' uses an unsupported value type: {value.ValueKind}.");
    }

    public static void EnsureSupportedPatchArrayValue(JsonElement value, string path)
    {
        int index = 0;

        foreach (JsonElement element in value.EnumerateArray())
        {
            EnsureSupportedPatchValue(element, $"{path}[{index}]");
            index++;
        }
    }

    private static bool IsArrayField(AssetsFieldInfo field)
    {
        return string.Equals(field.Name, "Array", StringComparison.Ordinal) ||
               string.Equals(field.TypeName, "Array", StringComparison.Ordinal);
    }

    private static string FormatArrayElementValue(AssetsFieldInfo element)
    {
        if (element.Value is null)
        {
            return FormatObjectFieldValue(element);
        }

        return string.Equals(element.TypeName, "string", StringComparison.OrdinalIgnoreCase)
            ? JsonElementFactory.String(element.Value).GetRawText()
            : element.Value;
    }

    private static bool ContainsArrayValue(
        IReadOnlyList<AssetsFieldInfo> currentFields,
        IReadOnlyList<JsonElement> elements,
        JsonElement value)
    {
        if (currentFields.Any(field => AssetFieldMatcher.MatchesFieldValue(field, value)))
        {
            return true;
        }

        return elements
            .Skip(currentFields.Count)
            .Any(element => JsonScalarValuesEqual(element, value));
    }

    private static bool JsonScalarValuesEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            right.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return left.GetBoolean() == right.GetBoolean();
        }

        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            return left.TryGetDouble(out double leftNumber) &&
                   right.TryGetDouble(out double rightNumber) &&
                   Math.Abs(leftNumber - rightNumber) <= 0.00001d;
        }

        return left.ValueKind == right.ValueKind &&
               string.Equals(JsonUtils.FormatElementValue(left), JsonUtils.FormatElementValue(right),
                   StringComparison.Ordinal);
    }

    private static JsonElement CreateJsonElementFromArrayElementField(AssetsFieldInfo field)
    {
        string value = field.Value ?? throw new InvalidOperationException(
            $"Array field '{field.Name}' contains a non-scalar element.");

        if (string.Equals(field.TypeName, "string", StringComparison.OrdinalIgnoreCase))
        {
            return JsonElementFactory.String(value);
        }

        if (IsBooleanType(field.TypeName))
        {
            if (bool.TryParse(value, out bool boolean))
            {
                return JsonElementFactory.Boolean(boolean);
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long booleanInteger))
            {
                return JsonElementFactory.Boolean(booleanInteger != 0);
            }
        }

        if (IsUnsignedIntegerType(field.TypeName) &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong unsignedInteger))
        {
            return JsonElementFactory.Number(unsignedInteger);
        }

        if (IsSignedIntegerType(field.TypeName) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signedInteger))
        {
            return JsonElementFactory.Number(signedInteger);
        }

        if (IsFloatingPointType(field.TypeName) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatingPoint))
        {
            return JsonElementFactory.Number(floatingPoint);
        }

        return JsonElementFactory.String(value);
    }

    private static bool IsBooleanType(string typeName)
    {
        return string.Equals(typeName, "bool", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeName, "boolean", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSignedIntegerType(string typeName)
    {
        return typeName.Equals("int", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("short", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("long", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("int", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("sint", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnsignedIntegerType(string typeName)
    {
        return typeName.Equals("byte", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("uint", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFloatingPointType(string typeName)
    {
        return string.Equals(typeName, "float", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeName, "double", StringComparison.OrdinalIgnoreCase);
    }
}
