using System.Collections.ObjectModel;

namespace UnityAssetsPatcher.Application.Repository;

public sealed record RepositoryMetadata(int FormatVersion, string RepositoryId);

internal static class RepositoryCollections
{
    public static IReadOnlyList<T> Copy<T>(IEnumerable<T?> values, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        T?[] nullableValues = [.. values];

        return nullableValues.Any(value => value is null)
            ? throw new ArgumentException("Backup collections cannot contain null entries.", parameterName)
            : Array.AsReadOnly([.. nullableValues.Select(value => value!)]);
    }

    public static IReadOnlyList<string>? CopyOptional(IEnumerable<string?>? values, string parameterName)
    {
        if (values is null)
        {
            return null;
        }

        string?[] nullableValues = [.. values];

        return nullableValues.Any(value => value is null)
            ? throw new ArgumentException("Backup collections cannot contain null entries.", parameterName)
            : new ReadOnlyCollection<string>([.. nullableValues.Select(value => value!)]);
    }
}
