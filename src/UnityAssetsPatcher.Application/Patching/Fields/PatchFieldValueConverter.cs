using System.Text;
using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Json;

namespace UnityAssetsPatcher.Application.Patching.Fields;

public static class PatchFieldValueConverter
{
    public static AssetField? Child(AssetField field, string name)
    {
        return field.FindChild(name);
    }

    public static bool IsJsonArrayPatchValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Array &&
               !JsonUtils.TryGetObjectValue(value, out _);
    }

    public static AssetField? ResolveArrayField(AssetField? field)
    {
        return AssetFieldNavigator.ResolveArray(field);
    }

    public static string ResolveArrayFieldPath(
        string fieldPath,
        AssetField? field,
        AssetField? arrayField)
    {
        return field is not null && arrayField is not null && !ReferenceEquals(field, arrayField)
            ? $"{fieldPath}.{arrayField.Name}"
            : fieldPath;
    }

    public static IReadOnlyList<AssetField> GetArrayElementFields(AssetField arrayField)
    {
        return AssetFieldNavigator.GetArrayElements(arrayField);
    }

    public static JsonElement GetObjectPropertyOrDefault(JsonElement value, string propertyName)
    {
        return JsonUtils.TryGetObjectValue(value, out JsonElement objectValue) &&
               objectValue.TryGetProperty(propertyName, out JsonElement propertyValue)
            ? propertyValue.Clone()
            : value;
    }

    public static string FormatObjectFieldValue(AssetField field)
    {
        string properties = string.Join(", ", field.Children
            .Where(child => child.Value is not null)
            .Select(child => $"{child.Name}: {child.Value}"));

        return properties.Length == 0 ? "<missing>" : $"{{ {properties} }}";
    }

    public static string FormatArrayFieldValue(AssetField arrayField)
    {
        string elements = string.Join(", ", GetArrayElementFields(arrayField).Select(FormatArrayElementValue));

        return $"[{elements}]";
    }

    public static JsonElement CreateAddArrayValue(
        AssetField arrayField,
        JsonElement value,
        out bool changed)
    {
        var currentFields = GetArrayElementFields(arrayField);
        var elements = currentFields
            .Select(CreateJsonElementFromArrayElementField)
            .ToList();
        changed = false;

        foreach (JsonElement element in value.EnumerateArray()
                     .Where(element => !ContainsArrayValue(currentFields, elements, element)))
        {
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

    private static string FormatArrayElementValue(AssetField element)
    {
        if (element.Value is null)
        {
            return FormatObjectFieldValue(element);
        }

        return element.Value is AssetFieldValue.String
            ? FormatJsonStringLiteral(element.Value.ToInvariantString())
            : element.Value.ToInvariantString();
    }

    private static string FormatJsonStringLiteral(string value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStringValue(value);
        }

        return stream.TryGetBuffer(out var buffer)
            ? Encoding.UTF8.GetString(buffer.AsSpan())
            : Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool ContainsArrayValue(
        IReadOnlyList<AssetField> currentFields,
        IReadOnlyList<JsonElement> elements,
        JsonElement value)
    {
        if (currentFields.Any(field => AssetFieldMatcher.MatchesValue(field, value)))
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

    private static JsonElement CreateJsonElementFromArrayElementField(AssetField field)
    {
        AssetFieldValue value = field.Value ?? throw new InvalidOperationException(
            $"Array field '{field.Name}' contains a non-scalar element.");

        return value switch
        {
            AssetFieldValue.String stringValue => JsonElementFactory.String(stringValue.Value),
            AssetFieldValue.Boolean boolValue => JsonElementFactory.Boolean(boolValue.Value),
            AssetFieldValue.Int64 integerValue => JsonElementFactory.Number(integerValue.Value),
            AssetFieldValue.UInt64 integerValue => JsonElementFactory.Number(integerValue.Value),
            AssetFieldValue.Float floatValue => JsonElementFactory.Number(floatValue.Value),
            AssetFieldValue.Double doubleValue => JsonElementFactory.Number(doubleValue.Value),
            _ => throw new InvalidOperationException($"Unsupported scalar value type '{value.GetType().Name}'."),
        };
    }
}
