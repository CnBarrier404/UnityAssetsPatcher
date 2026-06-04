using AssetsTools.NET;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

internal static class FieldTreeMapper
{
    public static AssetsFieldInfo Map(AssetTypeValueField field)
    {
        return new AssetsFieldInfo(
            field.FieldName,
            field.TypeName,
            field.Value?.ToString(),
            field.Children.Select(Map).ToArray());
    }
}
