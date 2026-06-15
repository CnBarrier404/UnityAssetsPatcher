using System.Text.Json;
using AssetsTools.NET;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.AssetsTools;

internal static class AssetsFieldWriter
{
    private static readonly Dictionary<AssetValueType, Action<AssetTypeValueField, JsonElement>> ScalarWriters = new()
    {
        [AssetValueType.Bool] = WriteBool,
        [AssetValueType.Int8] = WriteInt8,
        [AssetValueType.UInt8] = WriteUInt8,
        [AssetValueType.Int16] = WriteInt16,
        [AssetValueType.UInt16] = WriteUInt16,
        [AssetValueType.Int32] = WriteInt32,
        [AssetValueType.UInt32] = WriteUInt32,
        [AssetValueType.Int64] = WriteInt64,
        [AssetValueType.UInt64] = WriteUInt64,
        [AssetValueType.Float] = WriteFloat,
        [AssetValueType.Double] = WriteDouble,
        [AssetValueType.String] = WriteString,
    };

    public static void WriteJsonValue(AssetTypeValueField field, JsonElement value)
    {
        if (IsJsonArrayPatchValue(value))
        {
            WriteJsonArray(field, value);

            return;
        }

        AssetValueType valueType = GetScalarValueType(field);
        WriteScalarJsonValue(field, value, valueType);
    }

    private static AssetValueType GetScalarValueType(AssetTypeValueField field)
    {
        return field.Value?.ValueType ??
               throw new InvalidOperationException($"Field '{field.FieldName}' is not a scalar value.");
    }

    private static void WriteScalarJsonValue(
        AssetTypeValueField field,
        JsonElement value,
        AssetValueType valueType)
    {
        if (!ScalarWriters.TryGetValue(valueType, out var writeScalar))
        {
            throw new InvalidOperationException(
                $"Field '{field.FieldName}' has unsupported value type: {valueType}.");
        }

        writeScalar(field, value);
    }

    private static bool IsJsonArrayPatchValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Array &&
               !JsonUtils.TryGetObjectValue(value, out _);
    }

    private static void WriteJsonArray(AssetTypeValueField field, JsonElement value)
    {
        if (field.Value?.ValueType != AssetValueType.Array)
        {
            throw new InvalidOperationException($"Field '{field.FieldName}' is not an array value.");
        }

        var values = value.EnumerateArray().ToArray();

        if (field.Children.Count == 0 && values.Length > 0)
        {
            throw new InvalidOperationException(
                $"Cannot assign a non-empty array to field '{field.FieldName}' because it has no existing element template to clone.");
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (index == field.Children.Count)
            {
                field.Children.Add(field.Children[^1].Clone());
            }

            WriteJsonValue(field.Children[index], values[index]);
        }

        if (field.Children.Count > values.Length)
        {
            field.Children.RemoveRange(values.Length, field.Children.Count - values.Length);
        }

        AssetTypeArrayInfo arrayInfo = field.AsArray;
        arrayInfo.size = values.Length;
        field.AsArray = arrayInfo;
    }

    private static void WriteBool(AssetTypeValueField field, JsonElement value)
    {
        field.AsBool = value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Undefined => throw ThrowError(field, value),
            JsonValueKind.Object => throw ThrowError(field, value),
            JsonValueKind.Array => throw ThrowError(field, value),
            JsonValueKind.String => throw ThrowError(field, value),
            JsonValueKind.Number => throw ThrowError(field, value),
            JsonValueKind.Null => throw ThrowError(field, value),
            _ => throw ThrowError(field, value)
        };
    }

    private static void WriteInt8(AssetTypeValueField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long result))
        {
            throw ThrowError(field, value);
        }

        checked
        {
            field.AsSByte = (sbyte)result;
        }
    }

    private static void WriteUInt8(AssetTypeValueField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt64(out ulong result))
        {
            throw ThrowError(field, value);
        }

        checked
        {
            field.AsByte = (byte)result;
        }
    }

    private static void WriteInt16(AssetTypeValueField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long result))
        {
            throw ThrowError(field, value);
        }

        checked
        {
            field.AsShort = (short)result;
        }
    }

    private static void WriteUInt16(AssetTypeValueField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt64(out ulong result))
        {
            throw ThrowError(field, value);
        }

        checked
        {
            field.AsUShort = (ushort)result;
        }
    }

    private static void WriteInt32(AssetTypeValueField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long result))
        {
            throw ThrowError(field, value);
        }

        checked
        {
            field.AsInt = (int)result;
        }
    }

    private static void WriteUInt32(AssetTypeValueField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt64(out ulong result))
        {
            throw ThrowError(field, value);
        }

        checked
        {
            field.AsUInt = (uint)result;
        }
    }

    private static void WriteInt64(AssetTypeValueField field, JsonElement value)
    {
        field.AsLong = value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long result)
            ? result
            : throw ThrowError(field, value);
    }

    private static void WriteUInt64(AssetTypeValueField field, JsonElement value)
    {
        field.AsULong = value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out ulong result)
            ? result
            : throw ThrowError(field, value);
    }

    private static void WriteFloat(AssetTypeValueField field, JsonElement value)
    {
        field.AsFloat = value.ValueKind == JsonValueKind.Number &&
                        value.TryGetSingle(out float result) &&
                        !float.IsInfinity(result)
            ? result
            : throw ThrowError(field, value);
    }

    private static void WriteDouble(AssetTypeValueField field, JsonElement value)
    {
        field.AsDouble = value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double result)
            ? result
            : throw ThrowError(field, value);
    }

    private static void WriteString(AssetTypeValueField field, JsonElement value)
    {
        field.AsString = value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw ThrowError(field, value);
    }

    private static InvalidOperationException ThrowError(AssetTypeValueField field, JsonElement value)
    {
        return new InvalidOperationException(
            $"Cannot assign {value.ValueKind} value '{JsonUtils.FormatElementValue(value)}' to field '{field.FieldName}' of type {field.Value?.ValueType}.");
    }
}
