using System.Globalization;

namespace UnityAssetsPatcher.Domain.Assets;

public enum AssetScalarKind
{
    Boolean,
    Int8,
    UInt8,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float,
    Double,
    String
}

public abstract record AssetScalarValue
{
    public abstract AssetScalarKind Kind { get; }

    public abstract string ToInvariantString();

    public sealed override string ToString()
    {
        return ToInvariantString();
    }

    public sealed record Boolean(bool Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.Boolean;

        public override string ToInvariantString()
        {
            return Value ? "True" : "False";
        }
    }

    public sealed record Int8(sbyte Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.Int8;

        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record UInt8(byte Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.UInt8;

        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record Int16(short Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.Int16;

        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record UInt16(ushort Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.UInt16;

        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record Int32(int Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.Int32;

        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record UInt32(uint Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.UInt32;

        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record Int64(long Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.Int64;

        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record UInt64(ulong Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.UInt64;

        public override string ToInvariantString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed record Float(float Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.Float;

        public override string ToInvariantString()
        {
            return Value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public sealed record Double(double Value) : AssetScalarValue
    {
        public override AssetScalarKind Kind => AssetScalarKind.Double;

        public override string ToInvariantString()
        {
            return Value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public sealed record String : AssetScalarValue
    {
        public string Value { get; }
        public override AssetScalarKind Kind => AssetScalarKind.String;

        public String(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Value = value;
        }

        public override string ToInvariantString()
        {
            return Value;
        }
    }
}
