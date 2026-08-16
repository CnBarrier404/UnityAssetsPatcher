using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal static class AssetFieldWriter
{
    public static bool Write(AssetTypeValueField field, AssetWriteValue value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);

        switch (value)
        {
            case AssetScalarWriteValue scalar:
                WriteScalar(field, scalar.Value);
                return false;
            case AssetScalarArrayWriteValue array:
                WriteArray(field, array);
                return true;
            default:
                throw new InvalidOperationException($"Unsupported asset write value: {value.GetType().Name}.");
        }
    }

    private static void WriteArray(AssetTypeValueField field, AssetScalarArrayWriteValue value)
    {
        if (field.Value?.ValueType != AssetValueType.Array)
        {
            throw new InvalidOperationException($"Field '{field.FieldName}' is not an array value.");
        }

        AssetTypeTemplateField elementTemplate = field.TemplateField.Children
                                                     .FirstOrDefault(child =>
                                                         string.Equals(child.Name, "data", StringComparison.Ordinal))
                                                 ?? throw new InvalidDataException(
                                                     $"Array field '{field.FieldName}' does not define " +
                                                     "an element template.");
        AssetValueType expectedType = ToAssetValueType(value.ElementKind);

        if (!elementTemplate.HasValue || elementTemplate.ValueType != expectedType)
        {
            throw new InvalidOperationException(
                $"Field '{field.FieldName}' expects array elements of type {elementTemplate.ValueType}, " +
                $"not {value.ElementKind}.");
        }

        while (field.Children.Count < value.Values.Count)
        {
            field.Children.Add(field.Children.Count > 0
                ? field.Children[^1].Clone()
                : CreateArrayElement(elementTemplate));
        }

        for (int index = 0; index < value.Values.Count; index++)
        {
            WriteScalar(field.Children[index], value.Values[index]);
        }

        if (field.Children.Count > value.Values.Count)
        {
            field.Children.RemoveRange(value.Values.Count, field.Children.Count - value.Values.Count);
        }

        AssetTypeArrayInfo arrayInfo = field.AsArray;
        arrayInfo.size = value.Values.Count;
        field.AsArray = arrayInfo;
    }

    private static AssetTypeValueField CreateArrayElement(AssetTypeTemplateField elementTemplate)
    {
        var element = new AssetTypeValueField();
        element.Read(
            new AssetTypeValue(elementTemplate.ValueType, CreateDefaultScalarValue(elementTemplate.ValueType)),
            elementTemplate,
            []);

        return element;
    }

    private static object CreateDefaultScalarValue(AssetValueType valueType)
    {
        return valueType switch
        {
            AssetValueType.Bool => false,
            AssetValueType.Int8 => (sbyte)0,
            AssetValueType.UInt8 => (byte)0,
            AssetValueType.Int16 => (short)0,
            AssetValueType.UInt16 => (ushort)0,
            AssetValueType.Int32 => 0,
            AssetValueType.UInt32 => 0u,
            AssetValueType.Int64 => 0L,
            AssetValueType.UInt64 => 0UL,
            AssetValueType.Float => 0f,
            AssetValueType.Double => 0d,
            AssetValueType.String => string.Empty,
            _ => throw new InvalidOperationException($"Unsupported scalar value type: {valueType}.")
        };
    }

    private static void WriteScalar(AssetTypeValueField field, AssetScalarValue value)
    {
        AssetValueType actualType = field.Value?.ValueType
                                    ?? throw new InvalidOperationException(
                                        $"Field '{field.FieldName}' is not a scalar value.");
        AssetValueType expectedType = ToAssetValueType(value.Kind);

        if (actualType != expectedType)
        {
            throw new InvalidOperationException(
                $"Field '{field.FieldName}' has type {actualType}, not {value.Kind}.");
        }

        switch (value)
        {
            case AssetScalarValue.Boolean boolean:
                field.AsBool = boolean.Value;
                break;
            case AssetScalarValue.Int8 int8:
                field.AsSByte = int8.Value;
                break;
            case AssetScalarValue.UInt8 uint8:
                field.AsByte = uint8.Value;
                break;
            case AssetScalarValue.Int16 int16:
                field.AsShort = int16.Value;
                break;
            case AssetScalarValue.UInt16 uint16:
                field.AsUShort = uint16.Value;
                break;
            case AssetScalarValue.Int32 int32:
                field.AsInt = int32.Value;
                break;
            case AssetScalarValue.UInt32 uint32:
                field.AsUInt = uint32.Value;
                break;
            case AssetScalarValue.Int64 int64:
                field.AsLong = int64.Value;
                break;
            case AssetScalarValue.UInt64 uint64:
                field.AsULong = uint64.Value;
                break;
            case AssetScalarValue.Float single when float.IsFinite(single.Value):
                field.AsFloat = single.Value;
                break;
            case AssetScalarValue.Double doubleValue when double.IsFinite(doubleValue.Value):
                field.AsDouble = doubleValue.Value;
                break;
            case AssetScalarValue.String text:
                field.AsString = text.Value;
                break;
            default:
                throw new InvalidOperationException($"Cannot write non-finite or unsupported {value.Kind} value.");
        }
    }

    private static AssetValueType ToAssetValueType(AssetScalarKind kind)
    {
        return kind switch
        {
            AssetScalarKind.Boolean => AssetValueType.Bool,
            AssetScalarKind.Int8 => AssetValueType.Int8,
            AssetScalarKind.UInt8 => AssetValueType.UInt8,
            AssetScalarKind.Int16 => AssetValueType.Int16,
            AssetScalarKind.UInt16 => AssetValueType.UInt16,
            AssetScalarKind.Int32 => AssetValueType.Int32,
            AssetScalarKind.UInt32 => AssetValueType.UInt32,
            AssetScalarKind.Int64 => AssetValueType.Int64,
            AssetScalarKind.UInt64 => AssetValueType.UInt64,
            AssetScalarKind.Float => AssetValueType.Float,
            AssetScalarKind.Double => AssetValueType.Double,
            AssetScalarKind.String => AssetValueType.String,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported scalar kind.")
        };
    }
}
