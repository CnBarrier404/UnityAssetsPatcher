using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal static class AssetFieldMapper
{
    public static AssetField Map(AssetTypeValueField field)
    {
        AssetScalarValue? value = ToScalarValue(field);

        if (value is not null)
        {
            return new AssetScalarField(field.FieldName, field.TypeName, value);
        }

        if (field.Value?.ValueType != AssetValueType.Array)
        {
            return new AssetObjectField(field.FieldName, field.TypeName, field.Children.Select(Map));
        }

        AssetTypeTemplateField elementTemplate = GetArrayElementTemplate(field.TemplateField);

        return new AssetArrayField(
            field.FieldName,
            field.TypeName,
            MapSchema(elementTemplate),
            field.Children.Select(Map));
    }

    public static AssetScalarValue? ToScalarValue(AssetTypeValueField field)
    {
        return field.Value?.ValueType switch
        {
            AssetValueType.Bool => new AssetScalarValue.Boolean(field.AsBool),
            AssetValueType.Int8 => new AssetScalarValue.Int8(field.AsSByte),
            AssetValueType.UInt8 => new AssetScalarValue.UInt8(field.AsByte),
            AssetValueType.Int16 => new AssetScalarValue.Int16(field.AsShort),
            AssetValueType.UInt16 => new AssetScalarValue.UInt16(field.AsUShort),
            AssetValueType.Int32 => new AssetScalarValue.Int32(field.AsInt),
            AssetValueType.UInt32 => new AssetScalarValue.UInt32(field.AsUInt),
            AssetValueType.Int64 => new AssetScalarValue.Int64(field.AsLong),
            AssetValueType.UInt64 => new AssetScalarValue.UInt64(field.AsULong),
            AssetValueType.Float => new AssetScalarValue.Float(field.AsFloat),
            AssetValueType.Double => new AssetScalarValue.Double(field.AsDouble),
            AssetValueType.String => new AssetScalarValue.String(field.AsString),
            _ => null,
        };
    }

    private static AssetFieldSchema MapSchema(AssetTypeTemplateField template)
    {
        if (TryMapScalarKind(template.ValueType, out AssetScalarKind scalarKind))
        {
            return new AssetScalarFieldSchema(template.Type, scalarKind);
        }

        if (template.ValueType != AssetValueType.Array && !template.IsArray)
        {
            return new AssetObjectFieldSchema(template.Type, template.Children.Select(MapSchema).ToArray());
        }

        AssetTypeTemplateField elementTemplate = GetArrayElementTemplate(template);

        return new AssetArrayFieldSchema(template.Type, MapSchema(elementTemplate));
    }

    private static AssetTypeTemplateField GetArrayElementTemplate(AssetTypeTemplateField template)
    {
        return template.Children.FirstOrDefault(child => string.Equals(child.Name, "data", StringComparison.Ordinal))
               ?? throw new InvalidDataException($"Array field '{template.Name}' does not define an element template.");
    }

    private static bool TryMapScalarKind(AssetValueType valueType, out AssetScalarKind kind)
    {
        switch (valueType)
        {
            case AssetValueType.Bool:
                kind = AssetScalarKind.Boolean;
                return true;
            case AssetValueType.Int8:
                kind = AssetScalarKind.Int8;
                return true;
            case AssetValueType.UInt8:
                kind = AssetScalarKind.UInt8;
                return true;
            case AssetValueType.Int16:
                kind = AssetScalarKind.Int16;
                return true;
            case AssetValueType.UInt16:
                kind = AssetScalarKind.UInt16;
                return true;
            case AssetValueType.Int32:
                kind = AssetScalarKind.Int32;
                return true;
            case AssetValueType.UInt32:
                kind = AssetScalarKind.UInt32;
                return true;
            case AssetValueType.Int64:
                kind = AssetScalarKind.Int64;
                return true;
            case AssetValueType.UInt64:
                kind = AssetScalarKind.UInt64;
                return true;
            case AssetValueType.Float:
                kind = AssetScalarKind.Float;
                return true;
            case AssetValueType.Double:
                kind = AssetScalarKind.Double;
                return true;
            case AssetValueType.String:
                kind = AssetScalarKind.String;
                return true;
            case AssetValueType.None:
            case AssetValueType.Array:
            case AssetValueType.ByteArray:
            case AssetValueType.ManagedReferencesRegistry:
            default:
                kind = default;
                return false;
        }
    }
}
