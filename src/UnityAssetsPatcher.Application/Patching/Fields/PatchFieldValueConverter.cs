using System.Text;
using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Json;

namespace UnityAssetsPatcher.Application.Patching.Fields;

public static class PatchFieldValueConverter
{
    private const double AppendedNumberTolerance = 0.00001d;
    private const int FloatComparisonMaxUlps = 16;

    public static AssetField? Child(AssetField field, string name)
    {
        return field.FindChild(name);
    }

    public static bool IsJsonArrayPatchValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Array && !JsonUtils.TryGetObjectValue(value, out _);
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
        var currentValues = new CurrentArrayValueIndex(currentFields);
        var appendedValues = new AppendedArrayValueIndex();
        var appendedElements = new List<JsonElement>();
        changed = false;

        foreach (JsonElement element in value.EnumerateArray()
                     .Where(element => !currentValues.Contains(element) && appendedValues.Add(element)))
        {
            appendedElements.Add(element);
            changed = true;
        }

        return JsonElementFactory.ArrayFromWriter(writer =>
        {
            foreach (AssetField field in currentFields)
            {
                WriteArrayElementValue(writer, field);
            }

            foreach (JsonElement element in appendedElements)
            {
                element.WriteTo(writer);
            }
        });
    }

    public static void EnsureSupportedPatchValue(JsonElement value, string path)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number
            or JsonValueKind.String)
        {
            return;
        }

        throw new PatchPlanningException(
            PatchDiagnosticCode.UnsupportedValue,
            $"Patch operation for field '{path}' uses an unsupported value type: {value.ValueKind}.");
    }

    public static void EnsureCompatiblePatchValue(AssetField target, JsonElement value, string path)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        switch (target)
        {
            case AssetScalarField scalar:
                EnsureCompatibleScalarPatchValue(scalar.Value.Kind, value, path);
                break;
            case AssetArrayField { ElementSchema: AssetScalarFieldSchema element }:
                EnsureCompatibleScalarArrayPatchValue(element.Kind, value, path);
                break;
            case AssetArrayField:
                throw CreateIncompatiblePatchValueException(path, "an array of scalar values", value);
            default:
                throw new PatchPlanningException(
                    PatchDiagnosticCode.InvalidPatchConfiguration,
                    $"Patch operation for field '{path}' targets a non-scalar field.");
        }
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

    private static void EnsureCompatibleScalarPatchValue(
        AssetScalarKind targetKind,
        JsonElement value,
        string path)
    {
        EnsureSupportedPatchValue(value, path);

        bool compatible = targetKind switch
        {
            AssetScalarKind.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            AssetScalarKind.Int8 => value.ValueKind == JsonValueKind.Number && value.TryGetSByte(out _),
            AssetScalarKind.UInt8 => value.ValueKind == JsonValueKind.Number && value.TryGetByte(out _),
            AssetScalarKind.Int16 => value.ValueKind == JsonValueKind.Number && value.TryGetInt16(out _),
            AssetScalarKind.UInt16 => value.ValueKind == JsonValueKind.Number && value.TryGetUInt16(out _),
            AssetScalarKind.Int32 => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            AssetScalarKind.UInt32 => value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out _),
            AssetScalarKind.Int64 => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            AssetScalarKind.UInt64 => value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out _),
            AssetScalarKind.Float => value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out _),
            AssetScalarKind.Double => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out _),
            AssetScalarKind.String => value.ValueKind == JsonValueKind.String,
            _ => false
        };

        if (!compatible)
        {
            throw CreateIncompatiblePatchValueException(path, $"asset scalar kind '{targetKind}'", value);
        }
    }

    private static void EnsureCompatibleScalarArrayPatchValue(
        AssetScalarKind elementKind,
        JsonElement value,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw CreateIncompatiblePatchValueException(path, "an array of scalar values", value);
        }

        int index = 0;

        foreach (JsonElement element in value.EnumerateArray())
        {
            EnsureCompatibleScalarPatchValue(elementKind, element, $"{path}[{index}]");
            index++;
        }
    }

    private static PatchPlanningException CreateIncompatiblePatchValueException(
        string path,
        string expected,
        JsonElement value)
    {
        string actual = value.ValueKind == JsonValueKind.Undefined
            ? value.ValueKind.ToString()
            : JsonUtils.FormatElementValue(value);
        var diagnostic = new PatchDiagnostic(
            PatchDiagnosticCode.InvalidPatchConfiguration,
            "",
            FieldPath: path,
            Expected: expected,
            Actual: actual,
            Detail: $"Patch operation for field '{path}' uses value {actual}, which is incompatible with {expected}.");

        return new PatchPlanningException(diagnostic);
    }

    private static string FormatArrayElementValue(AssetField element)
    {
        if (element.Value is null)
        {
            return FormatObjectFieldValue(element);
        }

        return element.Value is AssetScalarValue.String
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

    private static AssetScalarValue GetArrayElementValue(AssetField field)
    {
        return field.Value ?? throw new PatchPlanningException(
            PatchDiagnosticCode.InvalidPatchConfiguration,
            $"Array field '{field.Name}' contains a non-scalar element.");
    }

    private static void WriteArrayElementValue(Utf8JsonWriter writer, AssetField field)
    {
        AssetScalarValue value = GetArrayElementValue(field);

        switch (value)
        {
            case AssetScalarValue.String stringValue:
                writer.WriteStringValue(stringValue.Value);
                break;
            case AssetScalarValue.Boolean boolValue:
                writer.WriteBooleanValue(boolValue.Value);
                break;
            case AssetScalarValue.Int8 integerValue:
                writer.WriteNumberValue(integerValue.Value);
                break;
            case AssetScalarValue.UInt8 integerValue:
                writer.WriteNumberValue(integerValue.Value);
                break;
            case AssetScalarValue.Int16 integerValue:
                writer.WriteNumberValue(integerValue.Value);
                break;
            case AssetScalarValue.UInt16 integerValue:
                writer.WriteNumberValue(integerValue.Value);
                break;
            case AssetScalarValue.Int32 integerValue:
                writer.WriteNumberValue(integerValue.Value);
                break;
            case AssetScalarValue.UInt32 integerValue:
                writer.WriteNumberValue(integerValue.Value);
                break;
            case AssetScalarValue.Int64 integerValue:
                writer.WriteNumberValue(integerValue.Value);
                break;
            case AssetScalarValue.UInt64 integerValue:
                writer.WriteNumberValue(integerValue.Value);
                break;
            case AssetScalarValue.Float floatValue:
                writer.WriteNumberValue(floatValue.Value);
                break;
            case AssetScalarValue.Double doubleValue:
                writer.WriteNumberValue(doubleValue.Value);
                break;
            default:
                throw new PatchPlanningException(
                    PatchDiagnosticCode.InvalidPatchConfiguration,
                    $"Unsupported scalar value type '{value.GetType().Name}'.");
        }
    }

    private sealed class CurrentArrayValueIndex
    {
        private readonly HashSet<bool> _booleans = [];
        private readonly HashSet<string> _strings = new(StringComparer.Ordinal);
        private readonly HashSet<long> _signedIntegers = [];
        private readonly HashSet<ulong> _unsignedIntegers = [];
        private readonly HashSet<float> _floats = [];
        private readonly HashSet<int> _floatBits = [];
        private readonly HashSet<double> _doubles = [];

        public CurrentArrayValueIndex(IEnumerable<AssetField> fields)
        {
            foreach (AssetField field in fields)
            {
                Add(GetArrayElementValue(field));
            }
        }

        public bool Contains(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.True or JsonValueKind.False => _booleans.Contains(value.GetBoolean()),
                JsonValueKind.String => _strings.Contains(value.GetString() ?? string.Empty),
                JsonValueKind.Number => ContainsNumber(value),
                JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined => false,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void Add(AssetScalarValue value)
        {
            switch (value)
            {
                case AssetScalarValue.Boolean boolean:
                    _booleans.Add(boolean.Value);
                    break;
                case AssetScalarValue.String text:
                    _strings.Add(text.Value);
                    break;
                case AssetScalarValue.Int8 integer:
                    _signedIntegers.Add(integer.Value);
                    break;
                case AssetScalarValue.Int16 integer:
                    _signedIntegers.Add(integer.Value);
                    break;
                case AssetScalarValue.Int32 integer:
                    _signedIntegers.Add(integer.Value);
                    break;
                case AssetScalarValue.Int64 integer:
                    _signedIntegers.Add(integer.Value);
                    break;
                case AssetScalarValue.UInt8 integer:
                    _unsignedIntegers.Add(integer.Value);
                    break;
                case AssetScalarValue.UInt16 integer:
                    _unsignedIntegers.Add(integer.Value);
                    break;
                case AssetScalarValue.UInt32 integer:
                    _unsignedIntegers.Add(integer.Value);
                    break;
                case AssetScalarValue.UInt64 integer:
                    _unsignedIntegers.Add(integer.Value);
                    break;
                case AssetScalarValue.Float number:
                    _floats.Add(number.Value);

                    if (float.IsFinite(number.Value))
                    {
                        _floatBits.Add(BitConverter.SingleToInt32Bits(number.Value));
                    }

                    break;
                case AssetScalarValue.Double number:
                    _doubles.Add(number.Value);
                    break;
                default:
                    throw new PatchPlanningException(
                        PatchDiagnosticCode.InvalidPatchConfiguration,
                        $"Unsupported scalar value type '{value.GetType().Name}'.");
            }
        }

        private bool ContainsNumber(JsonElement value)
        {
            return (value.TryGetInt64(out long signedInteger) && _signedIntegers.Contains(signedInteger)) ||
                   (value.TryGetUInt64(out ulong unsignedInteger) && _unsignedIntegers.Contains(unsignedInteger)) ||
                   (value.TryGetSingle(out float single) && ContainsFloat(single)) ||
                   (value.TryGetDouble(out double number) && _doubles.Contains(number));
        }

        private bool ContainsFloat(float expected)
        {
            if (_floats.Contains(expected))
            {
                return true;
            }

            if (!float.IsFinite(expected))
            {
                return false;
            }

            int expectedBits = BitConverter.SingleToInt32Bits(expected);
            long firstBits = Math.Max(int.MinValue, (long)expectedBits - FloatComparisonMaxUlps);
            long lastBits = Math.Min(int.MaxValue, (long)expectedBits + FloatComparisonMaxUlps);

            for (long bits = firstBits; bits <= lastBits; bits++)
            {
                if (_floatBits.Contains((int)bits))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class AppendedArrayValueIndex
    {
        private readonly HashSet<bool> _booleans = [];
        private readonly HashSet<string> _strings = new(StringComparer.Ordinal);
        private readonly SortedSet<double> _numbers = [];

        public bool Add(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.True or JsonValueKind.False => _booleans.Add(value.GetBoolean()),
                JsonValueKind.String => _strings.Add(value.GetString() ?? string.Empty),
                JsonValueKind.Number => AddNumber(value),
                JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined => true,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private bool AddNumber(JsonElement value)
        {
            if (!value.TryGetDouble(out double number) || !double.IsFinite(number))
            {
                return true;
            }

            double lowerBound = Math.BitDecrement(number - AppendedNumberTolerance);
            double upperBound = Math.BitIncrement(number + AppendedNumberTolerance);

            return !_numbers.GetViewBetween(lowerBound, upperBound)
                .Any(existing => Math.Abs(existing - number) <= AppendedNumberTolerance) && _numbers.Add(number);
        }
    }
}
