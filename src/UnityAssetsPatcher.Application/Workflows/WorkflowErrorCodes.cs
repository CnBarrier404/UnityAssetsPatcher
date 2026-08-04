using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Workflows;

public static class WorkflowErrorCodes
{
    public static OperationErrorCode GameDirectoryRequired { get; } = new("game_directory.required");
    public static OperationErrorCode GameDirectoryNotFound { get; } = new("game_directory.not_found");
    public static OperationErrorCode AssetNotFound { get; } = new("asset.not_found");
    public static OperationErrorCode PatchPlanningFailed { get; } = new("patch.planning_failed");
    public static OperationErrorCode InstallRecordNotFound { get; } = new("install_record.not_found");
    public static OperationErrorCode FileIntegrityMismatch { get; } = new("install.file_integrity_mismatch");
    public static OperationErrorCode InstallPreviewStale { get; } = new("install.preview_stale");
    public static OperationErrorCode OperationAlreadyRunning { get; } = new("operation.already_running");
    public static OperationErrorCode RecoveryRequired { get; } = new("backup.recovery_required");
    public static OperationErrorCode BackupRepositoryUnsafe { get; } = new("backup.repository_unsafe");

    public static OperationErrorCode UnsupportedBackupRepositoryVersion { get; } =
        new("backup.unsupported_repository_version");
}
