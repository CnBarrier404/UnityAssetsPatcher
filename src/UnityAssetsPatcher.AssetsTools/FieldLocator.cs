using AssetsTools.NET;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

internal static class FieldLocator
{
    public static AssetTypeValueField? Find(AssetTypeValueField field, string path)
    {
        return AssetFieldPathNavigator.Find(
            field,
            path,
            static candidate => candidate.FieldName,
            static candidate => candidate.Children,
            static candidate => candidate.Value?.ToString());
    }
}
