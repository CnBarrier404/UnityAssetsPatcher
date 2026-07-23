using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public static class AssetFieldMapper
{
    public static AssetField Map(AssetTypeValueField field)
    {
        return new AssetField(
            field.FieldName,
            field.TypeName,
            MapValue(field),
            field.Children.Select(Map));
    }

    public static AssetFieldValue? MapValue(AssetTypeValueField field)
    {
        return field.Value?.ValueType switch
        {
            AssetValueType.Bool => new AssetFieldValue.Boolean(field.AsBool),
            AssetValueType.Int8 => new AssetFieldValue.Int64(field.AsSByte),
            AssetValueType.UInt8 => new AssetFieldValue.UInt64(field.AsByte),
            AssetValueType.Int16 => new AssetFieldValue.Int64(field.AsShort),
            AssetValueType.UInt16 => new AssetFieldValue.UInt64(field.AsUShort),
            AssetValueType.Int32 => new AssetFieldValue.Int64(field.AsInt),
            AssetValueType.UInt32 => new AssetFieldValue.UInt64(field.AsUInt),
            AssetValueType.Int64 => new AssetFieldValue.Int64(field.AsLong),
            AssetValueType.UInt64 => new AssetFieldValue.UInt64(field.AsULong),
            AssetValueType.Float => new AssetFieldValue.Float(field.AsFloat),
            AssetValueType.Double => new AssetFieldValue.Double(field.AsDouble),
            AssetValueType.String => new AssetFieldValue.String(field.AsString),
            _ => null,
        };
    }
}
