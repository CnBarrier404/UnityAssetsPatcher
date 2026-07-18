using AssetsTools.NET;
using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public static class AssetsFieldInfoMapper
{
    public static AssetsFieldInfo Map(AssetTypeValueField field)
    {
        return new AssetsFieldInfo(
            field.FieldName,
            field.TypeName,
            MapValue(field),
            field.Children.Select(Map).ToArray());
    }

    public static AssetFieldValue? MapValue(AssetTypeValueField field)
    {
        return field.Value?.ValueType switch
        {
            AssetValueType.Bool => new BoolAssetFieldValue(field.AsBool),
            AssetValueType.Int8 => new Int64AssetFieldValue(field.AsSByte),
            AssetValueType.UInt8 => new UInt64AssetFieldValue(field.AsByte),
            AssetValueType.Int16 => new Int64AssetFieldValue(field.AsShort),
            AssetValueType.UInt16 => new UInt64AssetFieldValue(field.AsUShort),
            AssetValueType.Int32 => new Int64AssetFieldValue(field.AsInt),
            AssetValueType.UInt32 => new UInt64AssetFieldValue(field.AsUInt),
            AssetValueType.Int64 => new Int64AssetFieldValue(field.AsLong),
            AssetValueType.UInt64 => new UInt64AssetFieldValue(field.AsULong),
            AssetValueType.Float => new FloatAssetFieldValue(field.AsFloat),
            AssetValueType.Double => new DoubleAssetFieldValue(field.AsDouble),
            AssetValueType.String => new StringAssetFieldValue(field.AsString),
            _ => null,
        };
    }
}
