namespace UnityAssetsPatcher.Domain.Assets;

public sealed record AssetInfo
{
    public AssetPathId PathId { get; }
    public string TypeName { get; }

    public AssetInfo(AssetPathId pathId, string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        PathId = pathId;
        TypeName = typeName;
    }
}
