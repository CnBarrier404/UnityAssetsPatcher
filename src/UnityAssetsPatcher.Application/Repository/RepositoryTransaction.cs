using System.Text.Json;
using System.Text.Json.Serialization;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Repository;

public enum RepositoryOperationKind
{
    Install,
    Uninstall
}

public enum RepositoryFileKind
{
    Assets,
    Payload
}

public sealed record RepositoryTransactionFile(
    RepositoryFileKind Kind,
    string RelativePath,
    FileIntegrity? Before,
    FileIntegrity? After,
    string? RollbackRelativePath = null,
    string? PreparedRelativePath = null);

public sealed record RepositoryTransaction(
    string RepositoryId,
    RepositoryOperationKind Kind,
    string InstallId,
    string GameInstanceFingerprint,
    IReadOnlyList<RepositoryTransactionFile> Files);

public static class RepositoryTransactionStore
{
    public const string FileName = "transaction.json";

    public static void Save(
        IFileSystemOperations fileSystemOperations,
        string transactionDirectory,
        RepositoryTransaction transaction)
    {
        RepositoryJsonStore.Save(
            fileSystemOperations,
            Path.Combine(transactionDirectory, FileName),
            transaction,
            RepositoryJsonContext.Default.RepositoryTransaction);
    }

    public static RepositoryTransaction Load(string transactionDirectory)
    {
        string path = Path.Combine(transactionDirectory, FileName);
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize(stream, RepositoryJsonContext.Default.RepositoryTransaction)
               ?? throw new InvalidOperationException($"Transaction could not be read: {path}");
    }
}

internal static class RepositoryJsonStore
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

public sealed class RepositoryOperationLock : IDisposable
{
    private readonly FileStream _stream;
    private readonly string _repositoryFile;

    private RepositoryOperationLock(FileStream stream, string repositoryFile)
    {
        _stream = stream;
        _repositoryFile = repositoryFile;
    }

    public static RepositoryOperationLock Acquire(string repositoryFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryFile);
        string normalizedRepositoryFile = TrustedPath.NormalizeAbsolutePath(repositoryFile);

        try
        {
            return new RepositoryOperationLock(new FileStream(normalizedRepositoryFile, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.DeleteOnClose
            }), normalizedRepositoryFile);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException("Another install, uninstall, or recovery operation is running.",
                exception);
        }
    }

    internal void EnsureHeldFor(string repositoryDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);

        if (_stream.SafeFileHandle.IsClosed)
        {
            throw new InvalidOperationException("The backup operation lock is no longer held.");
        }

        string expectedRepositoryFile = Path.Combine(
            TrustedPath.NormalizeAbsolutePath(repositoryDirectory),
            RepositoryService.LockFileName);

        if (!TrustedPath.PathsEqual(_repositoryFile, expectedRepositoryFile))
        {
            throw new InvalidOperationException("The backup operation lock belongs to another repository.");
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(RepositoryTransaction))]
[JsonSerializable(typeof(RepositoryMetadata))]
[JsonSerializable(typeof(BaseCatalog))]
[JsonSerializable(typeof(BaseFileEntry))]
[JsonSerializable(typeof(PayloadBaseEntry))]
[JsonSerializable(typeof(LayerRecord))]
[JsonSerializable(typeof(LayerPackageInfo))]
public sealed partial class RepositoryJsonContext : JsonSerializerContext;
