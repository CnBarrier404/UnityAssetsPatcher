using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Application.Backups;

public static class InstallSequenceAllocator
{
    public static long Allocate(
        IEnumerable<InstallRecord> records,
        string gameInstanceFingerprint,
        string repositoryId)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameInstanceFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        InstallRecord[] snapshot = [.. records];

        InstallRecordValidator.ValidateAll(snapshot, repositoryId);

        long maximum = snapshot
            .Where(record => string.Equals(
                record.GameInstanceFingerprint,
                gameInstanceFingerprint,
                StringComparison.Ordinal))
            .Select(record => record.InstallSequence)
            .DefaultIfEmpty(0)
            .Max();

        return checked(maximum + 1);
    }
}

public static class InstallLayerAnalyzer
{
    public static IReadOnlyList<BlockingInstallRecord> FindBlockingRecords(
        InstallRecord target,
        IEnumerable<InstallRecordEntry> activeRecords)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(activeRecords);

        InstallRecordValidator.Validate(target);

        var targetFiles = target.PatchedFiles
            .Select(file => NormalizeRelativePath(file.AssetsFileRelativePath))
            .ToHashSet(TrustedPath.PathComparer);

        return activeRecords
            .Where(entry =>
                string.Equals(
                    entry.Record.GameInstanceFingerprint,
                    target.GameInstanceFingerprint,
                    StringComparison.Ordinal) &&
                entry.Record.InstallSequence > target.InstallSequence)
            .Select(entry => new BlockingInstallRecord(
                entry.Record,
                entry.Record.PatchedFiles
                    .Select(file => NormalizeRelativePath(file.AssetsFileRelativePath))
                    .Where(targetFiles.Contains)
                    .Distinct(TrustedPath.PathComparer)
                    .OrderBy(path => path, TrustedPath.PathComparer)
                    .ToArray()))
            .Where(record => record.OverlappingAssetsFiles.Count > 0)
            .OrderByDescending(record => record.Record.InstallSequence)
            .ToArray();
    }

    private static string NormalizeRelativePath(string path)
    {
        _ = TrustedPath.TryNormalizeRelativePath(path, out string normalized);

        return normalized;
    }
}
