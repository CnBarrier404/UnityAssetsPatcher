using System.Text.Json.Serialization;

namespace UnityAssetsPatcher.Application.Backups;

public sealed record InstallRecord(
    string RepositoryId,
    string GameInstanceFingerprint,
    long InstallSequence,
    string Id,
    DateTimeOffset InstalledAt,
    string ModName,
    string ModVersion,
    string ModAuthor,
    string? GameName,
    IReadOnlyList<InstallRecordPatchedFile> PatchedFiles,
    IReadOnlyList<InstallRecordCopiedFile> CopiedFiles)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? OptionalGroups { get; init; }
}

public sealed record InstallRecordPatchedFile(
    string Target,
    string AssetsFileRelativePath,
    string BackupRelativePath,
    int AssetCount,
    int OperationCount,
    FileIntegrity InstalledFile,
    FileIntegrity BackupFile);

public sealed record InstallRecordCopiedFile(
    string Source,
    string DestinationRelativePath,
    FileIntegrity InstalledFile);

public sealed record InstallRecordEntry(string InstallDirectory, InstallRecord Record);

public sealed record BlockingInstallRecord(InstallRecord Record, IReadOnlyList<string> OverlappingAssetsFiles);

public static class InstallRecordValidator
{
    public static void Validate(InstallRecord record) => Validate(record, record.RepositoryId);

    public static void Validate(InstallRecord record, string repositoryId)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!string.Equals(record.RepositoryId, repositoryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Install record does not belong to this backup repository.");
        }

        if (string.IsNullOrWhiteSpace(record.GameInstanceFingerprint))
        {
            throw new InvalidOperationException("Install record game instance fingerprint must not be empty.");
        }

        if (record.InstallSequence <= 0)
        {
            throw new InvalidOperationException("Install record sequence must be positive.");
        }

        if (string.IsNullOrWhiteSpace(record.Id))
        {
            throw new InvalidOperationException("Install record ID must not be empty.");
        }

        if (record.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || record.Id is "." or "..")
        {
            throw new InvalidOperationException("Install record ID must be a safe path segment.");
        }

        var assetsPaths = new HashSet<string>(PathComparer);
        var backupPaths = new HashSet<string>(PathComparer);
        var payloadPaths = new HashSet<string>(PathComparer);

        foreach (InstallRecordPatchedFile file in record.PatchedFiles)
        {
            ValidateRelativePath(file.AssetsFileRelativePath, "assets file");
            ValidateRelativePath(file.BackupRelativePath, "backup file");
            if (!assetsPaths.Add(Normalize(file.AssetsFileRelativePath)) ||
                !backupPaths.Add(Normalize(file.BackupRelativePath)))
                throw new InvalidOperationException("Install record contains duplicate patched or backup paths.");
            ValidateIntegrity(file.InstalledFile, $"patched assets file {file.AssetsFileRelativePath}");
            ValidateIntegrity(file.BackupFile, $"backup file {file.BackupRelativePath}");
        }

        foreach (InstallRecordCopiedFile file in record.CopiedFiles)
        {
            ValidateRelativePath(file.DestinationRelativePath, "payload file");
            if (!payloadPaths.Add(Normalize(file.DestinationRelativePath)))
                throw new InvalidOperationException("Install record contains duplicate payload paths.");
            ValidateIntegrity(file.InstalledFile, $"payload file {file.DestinationRelativePath}");
        }
    }

    public static void ValidateAll(IEnumerable<InstallRecord> records, string repositoryId)
    {
        ArgumentNullException.ThrowIfNull(records);

        var seen = new HashSet<(string Fingerprint, long Sequence)>();

        foreach (InstallRecord record in records)
        {
            Validate(record, repositoryId);

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

    private static void ValidateRelativePath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"Install record {description} path is not trusted: {path}");
        }
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

public static class InstallSequenceAllocator
{
    public static long Allocate(IEnumerable<InstallRecord> records, string gameInstanceFingerprint)
    {
        var snapshot = records.ToArray();

        return Allocate(snapshot, gameInstanceFingerprint, snapshot.FirstOrDefault()?.RepositoryId ?? string.Empty);
    }

    public static long Allocate(
        IEnumerable<InstallRecord> records,
        string gameInstanceFingerprint,
        string repositoryId)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameInstanceFingerprint);

        var snapshot = records.ToArray();
        InstallRecordValidator.ValidateAll(snapshot, repositoryId);

        long maximum = snapshot
            .Where(record =>
                string.Equals(record.GameInstanceFingerprint, gameInstanceFingerprint, StringComparison.Ordinal))
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

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
