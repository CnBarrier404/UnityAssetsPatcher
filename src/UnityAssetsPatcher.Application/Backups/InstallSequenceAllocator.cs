namespace UnityAssetsPatcher.Application.Backups;

public static class InstallSequenceAllocator
{
    public static long Allocate(IEnumerable<InstallRecord> records, string gameInstanceFingerprint)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameInstanceFingerprint);

        var snapshot = records.ToArray();

        InstallRecordValidator.ValidateAll(snapshot);

        long maximum = snapshot
            .Where(record =>
                string.Equals(record.GameInstanceFingerprint, gameInstanceFingerprint, StringComparison.Ordinal))
            .Select(record => record.InstallSequence)
            .DefaultIfEmpty(0)
            .Max();

        return checked(maximum + 1);
    }
}
