namespace UnityAssetsPatcher.Domain.Assets;

public readonly record struct AssetPathId(long Value)
{
    public static implicit operator AssetPathId(long value)
    {
        return new AssetPathId(value);
    }

    public static implicit operator long(AssetPathId pathId)
    {
        return pathId.Value;
    }

    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
