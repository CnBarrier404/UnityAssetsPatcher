namespace UnityAssetsPatcher.Application.Backups;

public sealed record InstallRecordEntry(string InstallDirectory, InstallRecord Record);

public sealed record BlockingInstallRecord(InstallRecord Record, IReadOnlyList<string> OverlappingAssetsFiles);

public static class InstallLayerAnalyzer
{
    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static IReadOnlyList<BlockingInstallRecord> FindBlockingRecords(
        InstallRecord target,
        IEnumerable<InstallRecordEntry> activeRecords)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(activeRecords);
        var targetFiles = target.PatchedFiles
            .Select(file => NormalizeRelativePath(file.AssetsFileRelativePath))
            .ToHashSet(PathComparer);

        return activeRecords
            .Where(entry => entry.Record.GameInstanceFingerprint == target.GameInstanceFingerprint &&
                            entry.Record.InstallSequence > target.InstallSequence)
            .Select(entry => new BlockingInstallRecord(
                entry.Record,
                entry.Record.PatchedFiles
                    .Select(file => NormalizeRelativePath(file.AssetsFileRelativePath))
                    .Where(targetFiles.Contains)
                    .Distinct(PathComparer)
                    .OrderBy(path => path, PathComparer)
                    .ToArray()))
            .Where(blocker => blocker.OverlappingAssetsFiles.Count > 0)
            .OrderByDescending(blocker => blocker.Record.InstallSequence)
            .ToArray();
    }
}
