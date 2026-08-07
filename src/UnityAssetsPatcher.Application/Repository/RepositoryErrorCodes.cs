using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Repository;

public static class RepositoryErrorCodes
{
    public static OperationErrorCode OperationAlreadyRunning { get; } = new("operation.already_running");
    public static OperationErrorCode RecoveryRequired { get; } = new("backup.recovery_required");
    public static OperationErrorCode Unsafe { get; } = new("backup.repository_unsafe");
    public static OperationErrorCode UnsupportedVersion { get; } = new("backup.unsupported_repository_version");
}
