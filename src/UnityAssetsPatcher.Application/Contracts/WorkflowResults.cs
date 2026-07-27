using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InspectAssetSummary(long PathId, string TypeName, string? Name);

public sealed record InspectListResult(IReadOnlyList<InspectAssetSummary> Assets, int TotalCount);

public sealed record UninstallModResult(
    string InstallId,
    string ModName,
    string ModVersion,
    IReadOnlyList<UninstallRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallDeletedFileResult> DeletedFiles)
{
    public BackupRecoveryReport Recovery { get; init; } = BackupRecoveryReport.Clean;
}

public sealed record UninstallRestoredFileResult(
    string Target,
    string AssetsFilePath);

public sealed record UninstallDeletedFileResult(string DestinationPath, bool Deleted);

public sealed record UninstallPreviewResult(
    string InstallId,
    string ModName,
    string ModVersion,
    DateTimeOffset InstalledAt,
    string GameDirectory,
    bool CanUninstall,
    IReadOnlyList<UninstallBlockingModResult> BlockingMods,
    IReadOnlyList<UninstallPreviewRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallPreviewDeletedFileResult> DeletedFiles);

public sealed record UninstallPreviewRestoredFileResult(
    string Target,
    FileIntegrityStatus TargetStatus,
    FileIntegrityStatus BackupStatus);

public sealed record UninstallPreviewDeletedFileResult(
    string DestinationPath,
    FileIntegrityStatus Status);

public enum FileIntegrityStatus
{
    Matches,
    Missing,
    Modified,
    Unreadable,
}

public sealed record UninstallBlockingModResult(
    string ModName,
    string ModVersion,
    DateTimeOffset InstalledAt,
    IReadOnlyList<string> OverlappingAssetsFiles);

public sealed record PatchApplyResult(string OutputPath, string? BackupPath, int AssetCount, int OperationCount);

public sealed record PatchPreviewResult(
    IReadOnlyList<PatchPreviewAssetResult> Assets,
    PatchDiagnostic? Diagnostic = null)
{
    public bool CanApply => Diagnostic is null;
}

public sealed record PatchPreviewAssetResult(AssetInfo Asset, IReadOnlyList<PatchPreviewOperationResult> Operations);

public sealed record PatchPreviewOperationResult(
    string Path,
    string OldValue,
    string FromText,
    string ToText,
    bool WillChange);

public sealed record InstallRecordSummary(
    string InstallId,
    string ModName,
    string ModVersion,
    string? GameName,
    DateTimeOffset InstalledAt);

public enum BackupRepositoryStatus
{
    Clean,
    RecoveryRequired,
    Recovered,
    Locked,
}

public sealed record BackupRecoveryOperation(string Kind, string InstallId, string Action);

public enum BackupRecoveryPlanAction
{
    RollBack,
    CompleteCleanup,
}

public enum BackupRecoveryFileAction
{
    NoChange,
    Restore,
    Delete,
}

public sealed record BackupRecoveryFileChange(string RelativePath, BackupRecoveryFileAction Action);

public sealed record BackupRecoveryPreview(
    BackupRepositoryStatus Status,
    string? GameDirectory,
    string? Kind,
    string? InstallId,
    BackupRecoveryPlanAction? Action,
    bool CanRecover,
    IReadOnlyList<BackupRecoveryFileChange> Files,
    IReadOnlyList<BackupRecoveryIssue> Issues);

public enum BackupRecoveryIssueCode
{
    RepositoryUnsafe,
    RecoveryUnsafe,
    OperationFailed,
    UnexpectedFailure,
}

public sealed record BackupRecoveryIssue(BackupRecoveryIssueCode Code, string Path)
{
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();
}

public sealed record BackupRecoveryReport(
    BackupRepositoryStatus Status,
    IReadOnlyList<BackupRecoveryOperation> Operations,
    IReadOnlyList<BackupRecoveryIssue> Issues)
{
    public static BackupRecoveryReport Clean { get; } = new(BackupRepositoryStatus.Clean, [], []);
}

public sealed class BackupRecoveryException : InvalidOperationException
{
    public BackupRecoveryReport Recovery { get; }

    public BackupRecoveryException(string message, BackupRecoveryReport recovery, Exception? innerException = null)
        : base(message, innerException)
    {
        Recovery = recovery;
    }
}
