using System.Collections.ObjectModel;

namespace UnityAssetsPatcher.Application.Operations;

public sealed record OperationAdvice
{
    public OperationAdviceCode Code { get; }

    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public OperationAdvice(
        OperationAdviceCode code,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        Code = code;
        Parameters = CopyParameters(parameters);
    }

    private static ReadOnlyDictionary<string, object?> CopyParameters(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return ReadOnlyDictionary<string, object?>.Empty;
        }

        var copy = new Dictionary<string, object?>(parameters.Count, StringComparer.Ordinal);

        foreach ((string key, object? value) in parameters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(parameters));
            copy.Add(key, value);
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}
