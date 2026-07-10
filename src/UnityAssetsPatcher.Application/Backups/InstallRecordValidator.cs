namespace UnityAssetsPatcher.Application.Backups;

public static class InstallRecordValidator
{
    public const int CurrentFormatVersion = 2;

    public static void Validate(InstallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.FormatVersion != CurrentFormatVersion)
        {
            throw new NotSupportedException($"Unsupported install record format version: {record.FormatVersion}.");
        }

        if (string.IsNullOrWhiteSpace(record.GameInstanceFingerprint))
        {
            throw new InvalidOperationException("Install record game instance fingerprint must not be empty.");
        }

        if (record.InstallSequence <= 0)
        {
            throw new InvalidOperationException("Install record sequence must be positive.");
        }

        foreach (InstallRecordPatchedFile file in record.PatchedFiles)
        {
            ValidateIntegrity(file.InstalledFile, $"patched assets file {file.AssetsFileRelativePath}");
            ValidateIntegrity(file.BackupFile, $"backup file {file.BackupRelativePath}");
        }

        foreach (InstallRecordCopiedFile file in record.CopiedFiles)
        {
            ValidateIntegrity(file.InstalledFile, $"payload file {file.DestinationRelativePath}");
        }
    }

    public static void ValidateAll(IEnumerable<InstallRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var seen = new HashSet<(string Fingerprint, long Sequence)>();

        foreach (InstallRecord record in records)
        {
            Validate(record);

            if (!seen.Add((record.GameInstanceFingerprint, record.InstallSequence)))
            {
                throw new InvalidOperationException(
                    $"Duplicate install sequence {record.InstallSequence} for game instance {record.GameInstanceFingerprint}.");
            }
        }
    }

    private static void ValidateIntegrity(FileIntegrity integrity, string description)
    {
        if (integrity is null)
        {
            throw new InvalidOperationException($"Install record {description} integrity must not be null.");
        }

        if (integrity.Length < 0)
        {
            throw new InvalidOperationException($"Install record {description} length must not be negative.");
        }

        if (string.IsNullOrEmpty(integrity.Sha256) || integrity.Sha256.Length != 64 || integrity.Sha256.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException(
                $"Install record {description} SHA-256 must be 64 lowercase hexadecimal characters.");
        }
    }
}
