using System.Text.Json.Serialization;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Repository;

public enum PayloadBaseState
{
    [JsonStringEnumMemberName("present")]
    Present,

    [JsonStringEnumMemberName("absent")]
    Absent,
}

public sealed record BaseFileEntry
{
    public string RelativePath { get; }
    public FileIntegrity Integrity { get; }

    public BaseFileEntry(string relativePath, FileIntegrity integrity)
    {
        RelativePath = CompositionRepositoryModelValidation.NormalizeRelativePath(relativePath, nameof(relativePath));

        ArgumentNullException.ThrowIfNull(integrity);

        Integrity = integrity;
    }
}

public sealed record PayloadBaseEntry
{
    public string RelativePath { get; }
    public PayloadBaseState BaseState { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FileIntegrity? Integrity { get; }

    public PayloadBaseEntry(string relativePath, PayloadBaseState baseState, FileIntegrity? integrity = null)
    {
        RelativePath = CompositionRepositoryModelValidation.NormalizeRelativePath(relativePath, nameof(relativePath));

        if (!Enum.IsDefined(baseState))
        {
            throw new ArgumentOutOfRangeException(nameof(baseState), baseState, "Unsupported payload base state.");
        }

        if (baseState == PayloadBaseState.Present && integrity is null)
        {
            throw new ArgumentNullException(nameof(integrity), "A present payload base entry requires file integrity.");
        }

        if (baseState == PayloadBaseState.Absent && integrity is not null)
        {
            throw new ArgumentException("An absent payload base entry cannot contain file integrity.",
                nameof(integrity));
        }

        BaseState = baseState;
        Integrity = integrity;
    }
}

public sealed record BaseCatalog
{
    public string GameInstanceFingerprint { get; }
    public DateTimeOffset CapturedAt { get; }
    public IReadOnlyList<BaseFileEntry> AssetsFiles { get; }
    public IReadOnlyList<PayloadBaseEntry> PayloadTargets { get; }

    public BaseCatalog(
        string gameInstanceFingerprint,
        DateTimeOffset capturedAt,
        IReadOnlyList<BaseFileEntry> assetsFiles,
        IReadOnlyList<PayloadBaseEntry> payloadTargets)
    {
        GameInstanceFingerprint = CompositionRepositoryModelValidation.NormalizeIdentifier(
            gameInstanceFingerprint,
            nameof(gameInstanceFingerprint));
        AssetsFiles = RepositoryCollections.Copy(assetsFiles, nameof(assetsFiles));
        PayloadTargets = RepositoryCollections.Copy(payloadTargets, nameof(payloadTargets));

        CompositionRepositoryModelValidation.EnsureUniquePaths(
            AssetsFiles.Select(file => file.RelativePath),
            nameof(assetsFiles));
        CompositionRepositoryModelValidation.EnsureUniquePaths(
            PayloadTargets.Select(file => file.RelativePath),
            nameof(payloadTargets));

        CapturedAt = capturedAt;
    }
}

public sealed record LayerPackageInfo
{
    public string FileName { get; }
    public FileIntegrity Integrity { get; }

    public LayerPackageInfo(string fileName, FileIntegrity integrity)
    {
        FileName = CompositionRepositoryModelValidation.NormalizeFileName(fileName, nameof(fileName));

        ArgumentNullException.ThrowIfNull(integrity);

        Integrity = integrity;
    }
}

public sealed record LayerRecord
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
    public bool Enabled { get; }
    public LayerPackageInfo Package { get; }
    public IReadOnlyList<string> AssetsTargets { get; }
    public IReadOnlyList<string> PayloadTargets { get; }

    public LayerRecord(
        string repositoryId,
        string gameInstanceFingerprint,
        long installSequence,
        string id,
        DateTimeOffset installedAt,
        string modName,
        string modVersion,
        string modAuthor,
        string? gameName,
        IReadOnlyList<string>? optionalGroups,
        bool enabled,
        LayerPackageInfo package,
        IReadOnlyList<string> assetsTargets,
        IReadOnlyList<string> payloadTargets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        if (installSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(installSequence),
                installSequence,
                "Layer install sequence must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(modName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(modAuthor);
        ArgumentNullException.ThrowIfNull(package);

        RepositoryId = repositoryId;
        GameInstanceFingerprint = CompositionRepositoryModelValidation.NormalizeIdentifier(
            gameInstanceFingerprint,
            nameof(gameInstanceFingerprint));
        InstallSequence = installSequence;
        Id = CompositionRepositoryModelValidation.NormalizeIdentifier(id, nameof(id));
        InstalledAt = installedAt;
        ModName = modName;
        ModVersion = modVersion;
        ModAuthor = modAuthor;
        GameName = gameName;
        OptionalGroups = RepositoryCollections.CopyOptional(optionalGroups, nameof(optionalGroups));
        Enabled = enabled;
        Package = package;
        AssetsTargets = CompositionRepositoryModelValidation.NormalizeRelativePaths(
            RepositoryCollections.Copy(assetsTargets, nameof(assetsTargets)),
            nameof(assetsTargets));
        PayloadTargets = CompositionRepositoryModelValidation.NormalizeRelativePaths(
            RepositoryCollections.Copy(payloadTargets, nameof(payloadTargets)),
            nameof(payloadTargets));

        CompositionRepositoryModelValidation.EnsureUniquePaths(AssetsTargets, nameof(assetsTargets));
        CompositionRepositoryModelValidation.EnsureUniquePaths(PayloadTargets, nameof(payloadTargets));
        CompositionRepositoryModelValidation.EnsureUniquePaths(
            AssetsTargets.Concat(PayloadTargets),
            nameof(payloadTargets));
    }
}

public sealed record LayerRecordEntry(string LayerDirectory, LayerRecord Record);

internal static class CompositionRepositoryModelValidation
{
    public static string NormalizeRelativePath(string path, string parameterName)
    {
        return !TrustedPath.TryNormalizeRelativePath(path, out string normalizedPath)
            ? throw new ArgumentException($"The relative path is not trusted: '{path}'.", parameterName)
            : normalizedPath;
    }

    public static string NormalizeFileName(string fileName, string parameterName)
    {
        string normalizedPath = NormalizeRelativePath(fileName, parameterName);

        if (normalizedPath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException($"The file name must not contain a directory separator: '{fileName}'.",
                parameterName);
        }

        return normalizedPath;
    }

    public static string NormalizeIdentifier(string value, string parameterName)
    {
        string normalizedPath = NormalizeRelativePath(value, parameterName);

        return normalizedPath.IndexOfAny(['\\', '/', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            ? throw new ArgumentException($"The identifier must be a single path segment: '{value}'.", parameterName)
            : normalizedPath;
    }

    public static IReadOnlyList<string> NormalizeRelativePaths(IEnumerable<string> paths, string parameterName)
    {
        string[] normalizedPaths = [.. paths.Select(path => NormalizeRelativePath(path, parameterName))];

        return Array.AsReadOnly(normalizedPaths);
    }

    public static void EnsureUniquePaths(IEnumerable<string> paths, string parameterName)
    {
        var seen = new HashSet<string>(TrustedPath.PathComparer);

        foreach (string path in paths)
        {
            if (!seen.Add(path))
            {
                throw new ArgumentException($"The collection contains duplicate path entries: '{path}'.",
                    parameterName);
            }
        }
    }
}
