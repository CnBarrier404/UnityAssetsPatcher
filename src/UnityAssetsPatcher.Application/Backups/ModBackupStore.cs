using System.Text.Json;
using System.Text.Json.Serialization;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.IO;

namespace UnityAssetsPatcher.Application.Backups;

public sealed record InstallRecord(
    int FormatVersion,
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

public sealed class ModBackupStore
{
    public string BackupDirectory { get; }

    private readonly Func<DateTimeOffset> _now;

    private const string RecordFileName = "record.json";

    public ModBackupStore(string backupDirectory, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(backupDirectory);

        BackupDirectory = backupDirectory;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public string CreateInstallDirectory(string modName, string version)
    {
        Directory.CreateDirectory(BackupDirectory);

        string timestamp = _now().ToString("yyyyMMddHHmmss");
        string sanitizedName = SanitizePathSegment(modName, "UnknownMod");
        string sanitizedVersion = SanitizePathSegment(version, "UnknownVersion");
        string baseName = $"{timestamp}-{sanitizedName}-{sanitizedVersion}";
        string candidate = Path.Combine(BackupDirectory, baseName);

        for (int index = 1; Directory.Exists(candidate); index++)
        {
            candidate = Path.Combine(BackupDirectory, $"{baseName}.{index}");
        }

        Directory.CreateDirectory(candidate);

        return candidate;
    }

    public static string BackupFile(string sourcePath, string backupDirectory)
    {
        Directory.CreateDirectory(backupDirectory);

        string backupPath = Path.Combine(backupDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, backupPath, false);

        return backupPath;
    }

    public static void RestoreFile(string backupPath, string targetPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(targetPath)) ??
                           throw new InvalidOperationException($"Cannot resolve assets file directory: {targetPath}");
        string tempPath = Path.Combine(directory, $"{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(backupPath, tempPath, false);
            FileHelper.SafeMoveFile(tempPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public void Save(InstallRecord record, string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(installDirectory);

        InstallRecordValidator.Validate(record);
        Directory.CreateDirectory(installDirectory);
        string recordPath = GetRecordPath(installDirectory);
        string temporaryPath = $"{recordPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, record, ModInstallationJsonContext.Default.InstallRecord);
                stream.Flush(true);
            }

            FileHelper.SafeMoveFile(temporaryPath, recordPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public InstallRecord Load(string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(installDirectory);

        string recordPath = GetRecordPath(installDirectory);
        using FileStream stream = File.OpenRead(recordPath);

        InstallRecord record = JsonSerializer.Deserialize(stream, ModInstallationJsonContext.Default.InstallRecord) ??
                               throw new InvalidOperationException($"Install record could not be read: {recordPath}");
        InstallRecordValidator.Validate(record);

        return record;
    }

    public static void DeleteRecord(string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(installDirectory);

        File.Delete(GetRecordPath(installDirectory));
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        return ListRecords()
            .OrderByDescending(item => item.Record.InstallSequence)
            .Select(item => new InstallRecordSummary(
                item.Record.Id,
                item.Record.ModName,
                item.Record.ModVersion,
                item.Record.GameName,
                item.Record.InstalledAt))
            .ToArray();
    }

    public IReadOnlyList<InstallRecordEntry> ListRecords()
    {
        if (!Directory.Exists(BackupDirectory)) return [];

        var records = Directory
            .EnumerateFiles(BackupDirectory, RecordFileName, SearchOption.AllDirectories)
            .Where(path =>
                !(Path.GetDirectoryName(path) ?? string.Empty).Contains(".quarantine-", StringComparison.Ordinal))
            .Select(path => Path.GetDirectoryName(path) ??
                            throw new InvalidOperationException(
                                $"Cannot resolve install record directory: {path}"))
            .Select(dir => new InstallRecordEntry(dir, Load(dir)))
            .ToArray();
        InstallRecordValidator.ValidateAll(records.Select(entry => entry.Record));

        return records;
    }

    public void RecoverPendingTransactions()
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return;
        }

        foreach (string journalPath in Directory.EnumerateFiles(BackupDirectory, OperationJournalStore.FileName,
                     SearchOption.AllDirectories).ToArray())
        {
            string directory = Path.GetDirectoryName(journalPath)!;

            try
            {
                Recover(directory, OperationJournalStore.Load(directory));
            }
            catch (Exception exception)
            {
                Quarantine(directory, exception);
            }
        }

        foreach (string recordPath in Directory.EnumerateFiles(BackupDirectory, RecordFileName,
                         SearchOption.AllDirectories)
                     .Where(path => !(Path.GetDirectoryName(path) ?? string.Empty).Contains(
                         ".quarantine-", StringComparison.Ordinal))
                     .ToArray())
        {
            string directory = Path.GetDirectoryName(recordPath)!;

            try
            {
                _ = Load(directory);
            }
            catch (Exception exception)
            {
                Quarantine(directory, exception);
            }
        }
    }

    private static void Recover(string directory, OperationJournal journal)
    {
        if (journal.Kind == OperationKind.Install)
        {
            if (HasValidRecord(directory))
            {
                OperationJournalStore.Delete(directory);
                return;
            }

            foreach (JournalPayloadFile file in journal.PayloadFiles.Reverse())
            {
                if (File.Exists(file.DestinationPath))
                {
                    File.Delete(file.DestinationPath);
                }
            }

            foreach (JournalPatchedFile file in journal.PatchedFiles.Reverse())
            {
                if (File.Exists(file.BackupPath))
                {
                    RestoreFile(file.BackupPath, file.AssetsFilePath);
                }
            }

            Directory.Delete(directory, true);
            return;
        }

        if (journal.Phase == OperationPhase.Committed)
        {
            foreach (JournalPatchedFile file in journal.PatchedFiles)
            {
                if (file.RollbackPath is not null && File.Exists(file.RollbackPath))
                {
                    File.Delete(file.RollbackPath);
                }
            }

            Directory.Delete(directory, true);
            return;
        }

        foreach (JournalPatchedFile file in journal.PatchedFiles.Reverse())
        {
            if (file.RollbackPath is not null && File.Exists(file.RollbackPath))
            {
                RestoreFile(file.RollbackPath, file.AssetsFilePath);
            }
        }

        foreach (JournalPayloadFile file in journal.PayloadFiles.Reverse())
        {
            if (file.StagingPath is not null && File.Exists(file.StagingPath) && !File.Exists(file.DestinationPath))
            {
                RestoreFile(file.StagingPath, file.DestinationPath);
            }
        }

        foreach (JournalPatchedFile file in journal.PatchedFiles)
        {
            if (file.RollbackPath is not null && File.Exists(file.RollbackPath))
            {
                File.Delete(file.RollbackPath);
            }
        }

        foreach (JournalPayloadFile file in journal.PayloadFiles)
        {
            if (file.StagingPath is not null && File.Exists(file.StagingPath))
            {
                File.Delete(file.StagingPath);
            }
        }

        OperationJournalStore.Delete(directory);
    }

    private static bool HasValidRecord(string directory)
    {
        try
        {
            string path = GetRecordPath(directory);

            if (!File.Exists(path))
            {
                return false;
            }

            using FileStream stream = File.OpenRead(path);

            InstallRecord? record =
                JsonSerializer.Deserialize(stream, ModInstallationJsonContext.Default.InstallRecord);

            if (record is null)
            {
                return false;
            }

            InstallRecordValidator.Validate(record);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Quarantine(string directory, Exception exception)
    {
        string destination = $"{directory}.quarantine-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        Directory.Move(directory, destination);
        File.WriteAllText(Path.Combine(destination, "recovery-error.txt"), exception.ToString());
    }

    public BackupOperationLock AcquireOperationLock() => BackupOperationLock.Acquire(BackupDirectory);

    private static string GetRecordPath(string installDirectory)
    {
        return Path.Combine(installDirectory, RecordFileName);
    }

    private static string SanitizePathSegment(string value, string fallback)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = new(value
            .Where(character => !char.IsWhiteSpace(character) && !invalid.Contains(character))
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
