using System.Collections.ObjectModel;

namespace UnityAssetsPatcher.Application.Repository;

public sealed record RepositoryMetadata(int FormatVersion, string RepositoryId);

public sealed record RepositoryClearResult(int PreviousFormatVersion, int FormatVersion);

public sealed class RepositoryOperationLockedException : InvalidOperationException
{
    public RepositoryOperationLockedException(IOException innerException)
        : base("Another install, uninstall, or recovery operation is running.", innerException) { }
}

public sealed class UnsupportedRepositoryFormatException : NotSupportedException
{
    public int ActualVersion { get; }
    public int SupportedVersion { get; }

    public UnsupportedRepositoryFormatException(int actualVersion, int supportedVersion)
        : base($"Unsupported backup repository format: {actualVersion}.")
    {
        ActualVersion = actualVersion;
        SupportedVersion = supportedVersion;
    }
}

public sealed class RepositoryClearNotAllowedException : InvalidOperationException
{
    public RepositoryClearNotAllowedException()
        : base("The backup repository can only be cleared when its format is unsupported.") { }
}

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
