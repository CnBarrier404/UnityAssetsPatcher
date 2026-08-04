using System.Collections.ObjectModel;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Operations;

public sealed record OperationError
{
    public OperationErrorCode Code { get; }

    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public IReadOnlyList<OperationAdvice> Advice { get; }

    public BackupRecoveryReport? Recovery { get; }

    public OperationError(
        OperationErrorCode code,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyList<OperationAdvice>? advice = null,
        BackupRecoveryReport? recovery = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        Code = code;
        Parameters = CopyParameters(parameters);
        Advice = CopyAdvice(advice);
        Recovery = recovery;
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

    private static ReadOnlyCollection<OperationAdvice> CopyAdvice(IReadOnlyList<OperationAdvice>? advice)
    {
        if (advice is null || advice.Count == 0)
        {
            return [];
        }

        IEnumerable<object?> nullableAdvice = advice;

        return nullableAdvice.Any(item => item is null)
            ? throw new ArgumentException("Advice cannot contain null elements.", nameof(advice))
            : Array.AsReadOnly([.. advice]);
    }
}
