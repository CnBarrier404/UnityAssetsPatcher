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
