using System.Globalization;

namespace UnityAssetsPatcher.Core.Assets;

public abstract record AssetFieldValue
{
    public static implicit operator AssetFieldValue?(string? value) =>
        value is null ? null : new StringAssetFieldValue(value);

    public abstract string ToInvariantString();

    public sealed override string ToString() => ToInvariantString();

    public static AssetFieldValue FromInvariantString(string typeName, string value)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(value);

        if (AssetFieldTypeNames.IsString(typeName))
        {
            return new StringAssetFieldValue(value);
        }

        if (AssetFieldTypeNames.IsBoolean(typeName))
        {
            if (bool.TryParse(value, out bool boolean))
            {
                return new BoolAssetFieldValue(boolean);
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
            {
                return new BoolAssetFieldValue(integer != 0);
            }
        }

        if (AssetFieldTypeNames.IsUnsignedInteger(typeName) &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong unsignedInteger))
        {
            return new UInt64AssetFieldValue(unsignedInteger);
        }

        if (AssetFieldTypeNames.IsSignedInteger(typeName) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signedInteger))
        {
            return new Int64AssetFieldValue(signedInteger);
        }

        if (AssetFieldTypeNames.IsFloat(typeName) &&
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float single))
        {
            return new FloatAssetFieldValue(single);
        }

        if (AssetFieldTypeNames.IsDouble(typeName) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double @double))
        {
            return new DoubleAssetFieldValue(@double);
        }

        return new StringAssetFieldValue(value);
    }
}

public sealed record BoolAssetFieldValue(bool Value) : AssetFieldValue
{
    public override string ToInvariantString() => Value ? "True" : "False";
}

public sealed record StringAssetFieldValue(string Value) : AssetFieldValue
{
    public override string ToInvariantString() => Value;
}

public sealed record Int64AssetFieldValue(long Value) : AssetFieldValue
{
    public override string ToInvariantString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record UInt64AssetFieldValue(ulong Value) : AssetFieldValue
{
    public override string ToInvariantString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record FloatAssetFieldValue(float Value) : AssetFieldValue
{
    public override string ToInvariantString() => Value.ToString("R", CultureInfo.InvariantCulture);
}

public sealed record DoubleAssetFieldValue(double Value) : AssetFieldValue
{
    public override string ToInvariantString() => Value.ToString("R", CultureInfo.InvariantCulture);
}
