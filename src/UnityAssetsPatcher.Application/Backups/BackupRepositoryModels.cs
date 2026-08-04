using System.Collections.ObjectModel;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Backups;

public sealed record BackupRepositoryMetadata(int FormatVersion, string RepositoryId);

public sealed record InstallRecordEntry(string InstallDirectory, InstallRecord Record);

public sealed record BlockingInstallRecord(InstallRecord Record, IReadOnlyList<string> OverlappingAssetsFiles);

public sealed record InstallRecord
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
    public IReadOnlyList<InstallRecordPatchedFile> PatchedFiles { get; }
    public IReadOnlyList<InstallRecordCopiedFile> CopiedFiles { get; }
    public IReadOnlyList<string>? OptionalGroups { get; }

    public InstallRecord(
        string repositoryId,
        string gameInstanceFingerprint,
        long installSequence,
        string id,
        DateTimeOffset installedAt,
        string modName,
        string modVersion,
        string modAuthor,
        string? gameName,
        IEnumerable<InstallRecordPatchedFile?> patchedFiles,
        IEnumerable<InstallRecordCopiedFile?> copiedFiles,
        IEnumerable<string?>? optionalGroups = null)
    {
        ArgumentNullException.ThrowIfNull(repositoryId);
        ArgumentNullException.ThrowIfNull(gameInstanceFingerprint);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(modName);
        ArgumentNullException.ThrowIfNull(modVersion);
        ArgumentNullException.ThrowIfNull(modAuthor);

        RepositoryId = repositoryId;
        GameInstanceFingerprint = gameInstanceFingerprint;
        InstallSequence = installSequence;
        Id = id;
        InstalledAt = installedAt;
        ModName = modName;
        ModVersion = modVersion;
        ModAuthor = modAuthor;
        GameName = gameName;
        PatchedFiles = BackupCollections.Copy(patchedFiles, nameof(patchedFiles));
        CopiedFiles = BackupCollections.Copy(copiedFiles, nameof(copiedFiles));
        OptionalGroups = BackupCollections.CopyOptional(optionalGroups, nameof(optionalGroups));
    }
}

public sealed record InstallRecordPatchedFile
{
    public string Target { get; }
    public string AssetsFileRelativePath { get; }
    public string BackupRelativePath { get; }
    public int AssetCount { get; }
    public int OperationCount { get; }
    public FileIntegrity InstalledFile { get; }
    public FileIntegrity BackupFile { get; }

    public InstallRecordPatchedFile(
        string target,
        string assetsFileRelativePath,
        string backupRelativePath,
        int assetCount,
        int operationCount,
        FileIntegrity installedFile,
        FileIntegrity backupFile)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(assetsFileRelativePath);
        ArgumentNullException.ThrowIfNull(backupRelativePath);
        ArgumentNullException.ThrowIfNull(installedFile);
        ArgumentNullException.ThrowIfNull(backupFile);

        Target = target;
        AssetsFileRelativePath = assetsFileRelativePath;
        BackupRelativePath = backupRelativePath;
        AssetCount = assetCount;
        OperationCount = operationCount;
        InstalledFile = installedFile;
        BackupFile = backupFile;
    }
}

public sealed record InstallRecordCopiedFile
{
    public string Source { get; }
    public string DestinationRelativePath { get; }
    public FileIntegrity InstalledFile { get; }

    public InstallRecordCopiedFile(string source, string destinationRelativePath, FileIntegrity installedFile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destinationRelativePath);
        ArgumentNullException.ThrowIfNull(installedFile);

        Source = source;
        DestinationRelativePath = destinationRelativePath;
        InstalledFile = installedFile;
    }
}

internal static class BackupCollections
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
