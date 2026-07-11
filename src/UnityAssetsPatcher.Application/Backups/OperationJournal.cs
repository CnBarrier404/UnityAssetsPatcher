using System.Text.Json;
using UnityAssetsPatcher.Core.IO;

namespace UnityAssetsPatcher.Application.Backups;

public enum OperationKind
{
    Install,
    Uninstall
}

public enum OperationPhase
{
    Pending,
    AssetsChanged,
    PayloadChanged,
    Committed
}

public sealed record JournalPatchedFile(string AssetsFilePath, string BackupPath, string? RollbackPath = null);

public sealed record JournalPayloadFile(string DestinationPath, string? StagingPath = null);

public sealed record OperationJournal(
    int FormatVersion,
    OperationKind Kind,
    OperationPhase Phase,
    string GameDirectory,
    IReadOnlyList<JournalPatchedFile> PatchedFiles,
    IReadOnlyList<JournalPayloadFile> PayloadFiles);

public static class OperationJournalStore
{
    public const int CurrentFormatVersion = 1;
    public const string FileName = "pending.json";

    public static void Save(string installDirectory, OperationJournal journal)
    {
        Directory.CreateDirectory(installDirectory);
        string path = Path.Combine(installDirectory, FileName);
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, journal, ModInstallationJsonContext.Default.OperationJournal);
                stream.Flush(true);
            }

            FileHelper.SafeMoveFile(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static OperationJournal Load(string installDirectory)
    {
        string path = Path.Combine(installDirectory, FileName);
        using FileStream stream = File.OpenRead(path);
        OperationJournal journal = JsonSerializer.Deserialize(
                                       stream, ModInstallationJsonContext.Default.OperationJournal)
                                   ?? throw new InvalidOperationException(
                                       $"Operation journal could not be read: {path}");
        if (journal.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidOperationException($"Unsupported operation journal version: {journal.FormatVersion}");
        }

        return journal;
    }

    public static void Delete(string installDirectory) => File.Delete(Path.Combine(installDirectory, FileName));
}
