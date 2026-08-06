using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal static class RepositoryJsonMapper
{
    public static RepositoryMetadata Map(V1RepositoryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new RepositoryMetadata(
            document.FormatVersion,
            Require(document.RepositoryId, "repository ID"));
    }

    public static LegacyInstallRecord Map(V1InstallRecordDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        IReadOnlyList<V1InstallRecordPatchedFileDocument?> patchedFiles = document.PatchedFiles ??
                                                                          throw Invalid(
                                                                              "patched files collection is missing");

        IReadOnlyList<V1InstallRecordCopiedFileDocument?> copiedFiles = document.CopiedFiles ??
                                                                        throw Invalid(
                                                                            "copied files collection is missing");

        foreach (V1InstallRecordPatchedFileDocument? file in patchedFiles)
        {
            ValidatePatchedFile(file);
        }

        foreach (V1InstallRecordCopiedFileDocument? file in copiedFiles)
        {
            ValidateCopiedFile(file);
        }

        return new LegacyInstallRecord(
            Require(document.RepositoryId, "repository ID"),
            Require(document.GameInstanceFingerprint, "game instance fingerprint"),
            document.InstallSequence,
            RequireIdentifier(document.Id, "install ID"),
            document.InstalledAt,
            Require(document.ModName, "mod name"),
            Require(document.ModVersion, "mod version"),
            Require(document.ModAuthor, "mod author"),
            document.GameName,
            document.OptionalGroups);
    }

    public static V1RepositoryDocument Map(RepositoryMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new V1RepositoryDocument(metadata.FormatVersion, metadata.RepositoryId);
    }

    private static void ValidatePatchedFile(V1InstallRecordPatchedFileDocument? document)
    {
        if (document is null)
        {
            throw Invalid("patched files collection contains a null entry");
        }

        _ = Require(document.Target, "patch target");
        ValidateRelativePath(document.AssetsFileRelativePath, "assets file relative path");
        ValidateRelativePath(document.BackupRelativePath, "backup relative path");
        _ = Map(document.InstalledFile, "installed assets file integrity");
        _ = Map(document.BackupFile, "backup file integrity");
    }

    private static void ValidateCopiedFile(V1InstallRecordCopiedFileDocument? document)
    {
        if (document is null)
        {
            throw Invalid("copied files collection contains a null entry");
        }

        _ = Require(document.Source, "copied file source");
        ValidateRelativePath(document.DestinationRelativePath, "copied file destination");
        _ = Map(document.InstalledFile, "installed copied file integrity");
    }

    private static FileIntegrity Map(V1FileIntegrityDocument? document, string description)
    {
        if (document is null)
        {
            throw Invalid($"{description} is missing");
        }

        try
        {
            return new FileIntegrity(document.Length, Require(document.Sha256, $"{description} SHA-256"));
        }
        catch (ArgumentException exception)
        {
            throw Invalid($"{description} is invalid", exception);
        }
    }

    private static void ValidateRelativePath(string? path, string description)
    {
        string value = Require(path, description);

        if (!TrustedPath.TryNormalizeRelativePath(value, out _))
        {
            throw Invalid($"{description} is not trusted");
        }
    }

    private static string RequireIdentifier(string? value, string description)
    {
        string identifier = Require(value, description);

        if (!TrustedPath.TryNormalizeRelativePath(identifier, out string normalized) ||
            normalized.Contains(Path.DirectorySeparatorChar) ||
            normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            throw Invalid($"{description} is not trusted");
        }

        return normalized;
    }

    private static string Require(string? value, string description)
    {
        return value ?? throw Invalid($"{description} is missing");
    }

    private static InvalidDataException Invalid(string detail, Exception? innerException = null)
    {
        return new InvalidDataException($"Version 1 backup repository data is invalid: {detail}.", innerException);
    }
}
