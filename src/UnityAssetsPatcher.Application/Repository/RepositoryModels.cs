using System.Collections.ObjectModel;

namespace UnityAssetsPatcher.Application.Repository;

public sealed record RepositoryMetadata(int FormatVersion, string RepositoryId);

public sealed record LegacyInstallRecord
{
    public string RepositoryId { get; }
    public string GameInstanceFingerprint { get; }
    public long InstallSequence { get; }
    public string Id { get; }
    public DateTimeOffset InstalledAt { get; }
    public string ModName { get; }
    public string ModVersion { get; }
    public string ModAuthor { get; }
    public string? GameName { get; }
    public IReadOnlyList<string>? OptionalGroups { get; }

    public LegacyInstallRecord(
        string repositoryId,
        string gameInstanceFingerprint,
        long installSequence,
        string id,
        DateTimeOffset installedAt,
        string modName,
        string modVersion,
        string modAuthor,
        string? gameName,
        IEnumerable<string?>? optionalGroups = null)
    {
        ArgumentNullException.ThrowIfNull(repositoryId);
        ArgumentNullException.ThrowIfNull(gameInstanceFingerprint);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(modName);
        ArgumentNullException.ThrowIfNull(modVersion);
        ArgumentNullException.ThrowIfNull(modAuthor);

        if (installSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(installSequence));
        }

        RepositoryId = repositoryId;
        GameInstanceFingerprint = gameInstanceFingerprint;
        InstallSequence = installSequence;
        Id = id;
        InstalledAt = installedAt;
        ModName = modName;
        ModVersion = modVersion;
        ModAuthor = modAuthor;
        GameName = gameName;
        OptionalGroups = RepositoryCollections.CopyOptional(optionalGroups, nameof(optionalGroups));
    }
}

public sealed record LegacyInstallRecordEntry(string InstallDirectory, LegacyInstallRecord Record);

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
