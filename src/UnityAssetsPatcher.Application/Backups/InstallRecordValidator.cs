using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Application.Backups;

public static class InstallRecordValidator
{
    public static void Validate(InstallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Validate(record, record.RepositoryId);
    }

    public static void Validate(InstallRecord record, string repositoryId)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        if (!string.Equals(record.RepositoryId, repositoryId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Install record does not belong to this backup repository.");
        }

        if (string.IsNullOrWhiteSpace(record.GameInstanceFingerprint))
        {
            throw new InvalidDataException("Install record game instance fingerprint must not be empty.");
        }

        if (record.InstallSequence <= 0)
        {
            throw new InvalidDataException("Install record sequence must be positive.");
        }

        ValidateInstallId(record.Id);

        var assetsPaths = new HashSet<string>(TrustedPath.PathComparer);
        var backupPaths = new HashSet<string>(TrustedPath.PathComparer);
        var payloadPaths = new HashSet<string>(TrustedPath.PathComparer);

        if ((from file in record.PatchedFiles
                let assetsPath = ValidateRelativePath(file.AssetsFileRelativePath, "assets file")
                let backupPath = ValidateRelativePath(file.BackupRelativePath, "backup file")
                where !assetsPaths.Add(assetsPath) || !backupPaths.Add(backupPath)
                select assetsPath).Any())
        {
            throw new InvalidDataException("Install record contains duplicate patched or backup paths.");
        }

        if (record.CopiedFiles.Select(file => ValidateRelativePath(file.DestinationRelativePath, "payload file"))
            .Any(payloadPath => !payloadPaths.Add(payloadPath)))
        {
            throw new InvalidDataException("Install record contains duplicate payload paths.");
        }
    }

    public static void ValidateAll(IEnumerable<InstallRecord> records, string repositoryId)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        var sequences = new HashSet<(string Fingerprint, long Sequence)>();

        foreach (InstallRecord record in records)
        {
            Validate(record, repositoryId);

            if (!sequences.Add((record.GameInstanceFingerprint, record.InstallSequence)))
            {
                throw new InvalidDataException(
                    $"Duplicate install sequence {record.InstallSequence} for game instance " +
                    $"{record.GameInstanceFingerprint}.");
            }
        }
    }

    public static void ValidateInstallId(string installId)
    {
        if (!TrustedPath.TryNormalizeRelativePath(installId, out string normalized) ||
            normalized.Contains(Path.DirectorySeparatorChar))
        {
            throw new InvalidDataException($"Invalid install ID: {installId}");
        }
    }

    private static string ValidateRelativePath(string path, string description)
    {
        return !TrustedPath.TryNormalizeRelativePath(path, out string normalized)
            ? throw new InvalidDataException($"Install record {description} path is not trusted: {path}")
            : normalized;
    }
}
