using System.Text.Json;
using AssetsTools.NET;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.AssetsTools;

internal static class ValueWriter
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
               !AssetFieldMatcher.TryGetObjectValue(value, out _);
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
            _ => throw CreateTypeMismatch(field, value)
        };
    }

    private static void WriteInt8(AssetTypeValueField field, JsonElement value)
    {
        field.AsSByte = checked((sbyte)GetInt64(value, field));
    }

    private static void WriteUInt8(AssetTypeValueField field, JsonElement value)
    {
        field.AsByte = checked((byte)GetUInt64(value, field));
    }

    private static void WriteInt16(AssetTypeValueField field, JsonElement value)
    {
        field.AsShort = checked((short)GetInt64(value, field));
    }

    private static void WriteUInt16(AssetTypeValueField field, JsonElement value)
    {
        field.AsUShort = checked((ushort)GetUInt64(value, field));
    }

    private static void WriteInt32(AssetTypeValueField field, JsonElement value)
    {
        field.AsInt = checked((int)GetInt64(value, field));
    }

    private static void WriteUInt32(AssetTypeValueField field, JsonElement value)
    {
        field.AsUInt = checked((uint)GetUInt64(value, field));
    }

    private static void WriteInt64(AssetTypeValueField field, JsonElement value)
    {
        field.AsLong = GetInt64(value, field);
    }

    private static void WriteUInt64(AssetTypeValueField field, JsonElement value)
    {
        field.AsULong = GetUInt64(value, field);
    }

    private static void WriteFloat(AssetTypeValueField field, JsonElement value)
    {
        field.AsFloat = (float)GetDouble(value, field);
    }

    private static void WriteDouble(AssetTypeValueField field, JsonElement value)
    {
        field.AsDouble = GetDouble(value, field);
    }

    private static void WriteString(AssetTypeValueField field, JsonElement value)
    {
        field.AsString = value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw CreateTypeMismatch(field, value);
    }

    private static long GetInt64(JsonElement value, AssetTypeValueField field)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long result)
            ? result
            : throw CreateTypeMismatch(field, value);
    }

    private static ulong GetUInt64(JsonElement value, AssetTypeValueField field)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out ulong result)
            ? result
            : throw CreateTypeMismatch(field, value);
    }

    private static double GetDouble(JsonElement value, AssetTypeValueField field)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double result)
            ? result
            : throw CreateTypeMismatch(field, value);
    }

    private static InvalidOperationException CreateTypeMismatch(AssetTypeValueField field, JsonElement value)
    {
        return new InvalidOperationException(
            $"Cannot assign {value.ValueKind} value '{AssetFieldMatcher.FormatJsonValue(value)}' to field '{field.FieldName}' of type {field.Value?.ValueType}.");
    }
}
