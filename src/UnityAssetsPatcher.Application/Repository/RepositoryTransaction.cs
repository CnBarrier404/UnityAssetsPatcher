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
