using System.Globalization;

namespace UnityAssetsPatcher.Application.Assets;

public abstract record AssetFieldValue
{
    public abstract string ToInvariantString();

    public sealed override string ToString()
    {
        return ToInvariantString();
    }

    public sealed record Boolean(bool Value) : AssetFieldValue
    {
        public override string ToInvariantString()
        {
            return Value ? "True" : "False";
        }
    }

    public sealed record String(string Value) : AssetFieldValue
    {
        public override string ToInvariantString()
        {
            return Value;
        }
    }

    public sealed record Int64(long Value) : AssetFieldValue
    {
        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record UInt64(ulong Value) : AssetFieldValue
    {
        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record Float(float Value) : AssetFieldValue
    {
        public override string ToInvariantString()
        {
            return Value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public sealed record Double(double Value) : AssetFieldValue
    {
        public override string ToInvariantString()
        {
            return Value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
