using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Backups;

internal static class V1BackupMapper
{
    public static BackupRepositoryMetadata Map(V1RepositoryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new BackupRepositoryMetadata(
            document.FormatVersion,
            Require(document.RepositoryId, "repository ID"));
    }

    public static InstallRecord Map(V1InstallRecordDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        IReadOnlyList<V1InstallRecordPatchedFileDocument?> patchedFiles = document.PatchedFiles ??
                                                                          throw Invalid(
                                                                              "patched files collection is missing");

        IReadOnlyList<V1InstallRecordCopiedFileDocument?> copiedFiles = document.CopiedFiles ??
                                                                        throw Invalid(
                                                                            "copied files collection is missing");

        return new InstallRecord(
            Require(document.RepositoryId, "repository ID"),
            Require(document.GameInstanceFingerprint, "game instance fingerprint"),
            document.InstallSequence,
            Require(document.Id, "install ID"),
            document.InstalledAt,
            Require(document.ModName, "mod name"),
            Require(document.ModVersion, "mod version"),
            Require(document.ModAuthor, "mod author"),
            document.GameName,
            patchedFiles.Select(Map),
            copiedFiles.Select(Map),
            document.OptionalGroups);
    }

    public static V1RepositoryDocument Map(BackupRepositoryMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new V1RepositoryDocument(metadata.FormatVersion, metadata.RepositoryId);
    }

    public static V1InstallRecordDocument Map(InstallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new V1InstallRecordDocument(
            record.RepositoryId,
            record.GameInstanceFingerprint,
            record.InstallSequence,
            record.Id,
            record.InstalledAt,
            record.ModName,
            record.ModVersion,
            record.ModAuthor,
            record.GameName,
            [.. record.PatchedFiles.Select(Map)],
            [.. record.CopiedFiles.Select(Map)])
        {
            OptionalGroups = record.OptionalGroups is null ? null : [.. record.OptionalGroups],
        };
    }

    private static InstallRecordPatchedFile Map(V1InstallRecordPatchedFileDocument? document)
    {
        if (document is null)
        {
            throw Invalid("patched files collection contains a null entry");
        }

        return new InstallRecordPatchedFile(
            Require(document.Target, "patch target"),
            Require(document.AssetsFileRelativePath, "assets file relative path"),
            Require(document.BackupRelativePath, "backup relative path"),
            document.AssetCount,
            document.OperationCount,
            Map(document.InstalledFile, "installed assets file integrity"),
            Map(document.BackupFile, "backup file integrity"));
    }

    private static InstallRecordCopiedFile Map(V1InstallRecordCopiedFileDocument? document)
    {
        if (document is null)
        {
            throw Invalid("copied files collection contains a null entry");
        }

        return new InstallRecordCopiedFile(
            Require(document.Source, "copied file source"),
            Require(document.DestinationRelativePath, "copied file destination"),
            Map(document.InstalledFile, "installed copied file integrity"));
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

    private static V1InstallRecordPatchedFileDocument Map(InstallRecordPatchedFile file)
    {
        return new V1InstallRecordPatchedFileDocument(
            file.Target,
            file.AssetsFileRelativePath,
            file.BackupRelativePath,
            file.AssetCount,
            file.OperationCount,
            Map(file.InstalledFile),
            Map(file.BackupFile));
    }

    private static V1InstallRecordCopiedFileDocument Map(InstallRecordCopiedFile file)
    {
        return new V1InstallRecordCopiedFileDocument(
            file.Source,
            file.DestinationRelativePath,
            Map(file.InstalledFile));
    }

    private static V1FileIntegrityDocument Map(FileIntegrity integrity)
    {
        return new V1FileIntegrityDocument(integrity.Length, integrity.Sha256);
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
