namespace UnityAssetsPatcher.Core.Assets;

public sealed record AssetsInfo
{
    public AssetsInfo(long pathId, int typeId, string typeName, uint byteSize)
    {
        PathId = pathId;
        TypeId = typeId;
        TypeName = typeName;
        ByteSize = byteSize;
    }

    public long PathId { get; }
    public int TypeId { get; }
    public string TypeName { get; }
    public uint ByteSize { get; }
}
