using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public static class AssetFieldLocator
{
    public static AssetTypeValueField? Find(AssetTypeValueField field, string path)
    {
        return AssetFieldPath.Find(
            field,
            path,
            static candidate => candidate.FieldName,
            static candidate => candidate.Children,
            static candidate => AssetFieldMapper.MapValue(candidate)?.ToInvariantString(),
            static (candidate, name) => candidate.Children.Where(child =>
                string.Equals(child.FieldName, name, StringComparison.Ordinal)));
    }
}
