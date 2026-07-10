namespace UnityAssetsPatcher.Application.Backups;

public static class InstallRecordValidator
{
    public const int CurrentFormatVersion = 1;

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
}
