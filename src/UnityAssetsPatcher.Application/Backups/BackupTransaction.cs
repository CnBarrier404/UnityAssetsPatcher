using System.Text.Json;
using System.Text.Json.Serialization;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Backups;

public enum BackupOperationKind
{
    Install,
    Uninstall,
}

public enum BackupFileKind
{
    Assets,
    Payload,
}

public sealed record BackupTransactionFile(
    BackupFileKind Kind,
    string RelativePath,
    FileIntegrity? Before,
    FileIntegrity? After,
    string? RollbackRelativePath = null,
    string? PreparedRelativePath = null);

public sealed record BackupTransaction(
    string RepositoryId,
    BackupOperationKind Kind,
    string InstallId,
    string GameInstanceFingerprint,
    IReadOnlyList<BackupTransactionFile> Files);

public static class BackupTransactionStore
{
    public const string FileName = "transaction.json";

    public static void Save(
        IFileSystemOperations fileSystemOperations,
        string transactionDirectory,
        BackupTransaction transaction)
    {
        BackupJsonStore.Save(
            fileSystemOperations,
            Path.Combine(transactionDirectory, FileName),
            transaction,
            BackupJsonContext.Default.BackupTransaction);
    }

    public static BackupTransaction Load(string transactionDirectory)
    {
        string path = Path.Combine(transactionDirectory, FileName);
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize(stream, BackupJsonContext.Default.BackupTransaction)
               ?? throw new InvalidOperationException($"Transaction could not be read: {path}");
    }
}

internal static class BackupJsonStore
{
    public static void Save<T>(
        IFileSystemOperations fileSystemOperations,
        string path,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        fileSystemOperations.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        fileSystemOperations.WriteFile(path, stream => { JsonSerializer.Serialize(stream, value, typeInfo); });
    }
}

public sealed class BackupOperationLock : IDisposable
{
    private readonly FileStream _stream;

    private BackupOperationLock(FileStream stream)
    {
        _stream = stream;
    }

    public static BackupOperationLock Acquire(string repositoryFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryFile);
        try
        {
            return new BackupOperationLock(new FileStream(repositoryFile, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.DeleteOnClose,
            }));
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException("Another install, uninstall, or recovery operation is running.",
                exception);
        }
    }

    public void Dispose() => _stream.Dispose();
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(InstallRecord))]
[JsonSerializable(typeof(BackupTransaction))]
[JsonSerializable(typeof(BackupRepositoryMetadata))]
internal sealed partial class BackupJsonContext : JsonSerializerContext;
