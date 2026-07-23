using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetFieldLocator
{
    private readonly AssetFieldPathResolver<AssetTypeValueField> _resolver;

    public AssetFieldLocator(AssetTypeValueField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        _resolver = new AssetFieldPathResolver<AssetTypeValueField>(
            field,
            static candidate => candidate.FieldName,
            static candidate => candidate.Children,
            static candidate => AssetFieldMapper.MapValue(candidate)?.ToInvariantString(),
            static (candidate, name) => candidate.Children.Where(child =>
                string.Equals(child.FieldName, name, StringComparison.Ordinal)));
    }

    public AssetTypeValueField? Find(string path)
    {
        return _resolver.Find(path);
    }

    public void InvalidateStructure()
    {
        _resolver.InvalidateStructure();
    }

    public static AssetTypeValueField? Find(AssetTypeValueField field, string path)
    {
        return new AssetFieldLocator(field).Find(path);
    }
}
