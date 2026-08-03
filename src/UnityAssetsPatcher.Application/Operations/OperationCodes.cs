namespace UnityAssetsPatcher.Application.Operations;

public sealed record OperationErrorCode
{
    public string Value { get; }

    public OperationErrorCode(string value)
    {
        OperationCodeValidator.Validate(value, nameof(value));

        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}

public sealed record OperationAdviceCode
{
    public string Value { get; }

    public OperationAdviceCode(string value)
    {
        OperationCodeValidator.Validate(value, nameof(value));

        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}

internal static class OperationCodeValidator
{
    public static void Validate(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string[] segments = value.Split('.');

        if (segments.Length < 2 || segments.Any(segment => !IsValidSegment(segment)))
        {
            throw new ArgumentException(
                "Operation codes must contain at least two dot-separated lower_snake_case segments.",
                parameterName);
        }
    }

    private static bool IsValidSegment(string segment)
    {
        if (segment.Length == 0 || segment[0] is < 'a' or > 'z')
        {
            return false;
        }

        return segment.Skip(1).All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }
}
