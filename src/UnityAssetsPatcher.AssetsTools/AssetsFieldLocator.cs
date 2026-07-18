using AssetsTools.NET;
using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.AssetsTools;

internal static class AssetsFieldLocator
{
    public static AssetTypeValueField? Find(AssetTypeValueField field, string path)
    {
        return AssetFieldPathNavigator.Find(
            field,
            path,
            static candidate => candidate.FieldName,
            static candidate => candidate.Children,
            static candidate => AssetsFieldInfoMapper.MapValue(candidate)?.ToInvariantString());
    }
}
