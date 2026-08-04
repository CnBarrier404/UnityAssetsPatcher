namespace UnityAssetsPatcher.Application.Contracts;

public abstract record OperationResult<T>
{
    private protected OperationResult() { }
}

public sealed record OperationSucceeded<T>(T Value) : OperationResult<T>;

public sealed record OperationFailed<T>(OperationError Error) : OperationResult<T>;

public sealed record OperationError(OperationErrorCode Code)
{
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    public BackupRecoveryReport? Recovery { get; init; }
}

public enum OperationErrorCode
{
    FileNotFound,
    DirectoryNotFound,
    AccessDenied,
    FileSystemFailure,
    InvalidManifest,
    UnsupportedManifestVersion,
    InvalidModPackage,
    GameDirectoryRequired,
    GameDirectoryNotFound,
    AssetNotFound,
    PatchPlanningFailed,
    InstallRecordNotFound,
    FileIntegrityMismatch,
    InstallPreviewStale,
    OperationAlreadyRunning,
    RecoveryRequired,
    BackupRepositoryUnsafe,
    UnsupportedBackupRepositoryVersion,
}
