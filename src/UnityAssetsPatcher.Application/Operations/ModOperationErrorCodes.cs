namespace UnityAssetsPatcher.Application.Operations;

public static class ModOperationErrorCodes
{
    public static OperationErrorCode InstallRecordNotFound { get; } = new("install_record.not_found");
    public static OperationErrorCode FileIntegrityMismatch { get; } = new("install.file_integrity_mismatch");
    public static OperationErrorCode InstallPreviewStale { get; } = new("install.preview_stale");
}
