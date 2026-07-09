using System.Text.Json;
using System.Text.Json.Serialization;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.IO;

namespace UnityAssetsPatcher.Application.Backups;

public sealed class ModBackupStore
{
    private const string RecordFileName = "record.json";
    private readonly string _backupDirectory;
    private readonly Func<DateTimeOffset> _now;

    public ModBackupStore(string backupDirectory, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(backupDirectory);

        _backupDirectory = backupDirectory;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public string CreateInstallDirectory(string modName, string version)
    {
        Directory.CreateDirectory(_backupDirectory);

        string timestamp = _now().ToString("yyyyMMddHHmmss");
        string sanitizedName = SanitizePathSegment(modName, "UnknownMod");
        string sanitizedVersion = SanitizePathSegment(version, "UnknownVersion");
        string baseName = $"{timestamp}-{sanitizedName}-{sanitizedVersion}";
        string candidate = Path.Combine(_backupDirectory, baseName);

        for (int index = 1; Directory.Exists(candidate); index++)
        {
            candidate = Path.Combine(_backupDirectory, $"{baseName}.{index}");
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

        Directory.CreateDirectory(installDirectory);
        string recordPath = GetRecordPath(installDirectory);
        string json = JsonSerializer.Serialize(record, ModInstallationJsonContext.Default.InstallRecord);
        File.WriteAllText(recordPath, json);
    }

    public InstallRecord Load(string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(installDirectory);

        string recordPath = GetRecordPath(installDirectory);
        using FileStream stream = File.OpenRead(recordPath);

        return JsonSerializer.Deserialize(stream, ModInstallationJsonContext.Default.InstallRecord) ??
               throw new InvalidOperationException($"Install record could not be read: {recordPath}");
    }

    public static void DeleteRecord(string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(installDirectory);

        File.Delete(GetRecordPath(installDirectory));
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        if (!Directory.Exists(_backupDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_backupDirectory, RecordFileName, SearchOption.AllDirectories)
            .Select(path => Path.GetDirectoryName(path) ??
                            throw new InvalidOperationException(
                                $"Cannot resolve install record directory: {path}"))
            .Select(dir => new
            {
                InstallDirectory = dir,
                Record = Load(dir)
            })
            .OrderByDescending(item => item.Record.InstalledAt)
            .Select(item => new InstallRecordSummary(
                item.InstallDirectory,
                item.Record.ModName,
                item.Record.ModVersion,
                item.Record.GameDirectory,
                item.Record.InstalledAt))
            .ToArray();
    }

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

public sealed record InstallRecord(
    string Id,
    DateTimeOffset InstalledAt,
    string ModName,
    string ModVersion,
    string ModAuthor,
    string? PackagePath,
    string GameDirectory,
    IReadOnlyList<InstallRecordPatchedFile> PatchedFiles,
    IReadOnlyList<InstallRecordCopiedFile> CopiedFiles)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? OptionalGroups { get; init; }
}

public sealed record InstallRecordPatchedFile(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);

public sealed record InstallRecordCopiedFile(
    string Source,
    string DestinationPath,
    bool Exists);
