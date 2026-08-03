using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal sealed class AssetFieldLocator
{
    private readonly AssetFieldPathResolver<AssetTypeValueField> _resolver;

    public AssetFieldLocator(AssetTypeValueField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        _resolver = new AssetFieldPathResolver<AssetTypeValueField>(
            field,
            static candidate => candidate.FieldName,
            static candidate => candidate.Children,
            static candidate => AssetFieldMapper.ToScalarValue(candidate)?.ToInvariantString());
    }

    public AssetTypeValueField? Find(AssetFieldPath path)
    {
        return _resolver.Find(path);
    }

    public void InvalidateStructure()
    {
        _resolver.InvalidateStructure();
    }
}
