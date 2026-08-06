using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InspectAssetSummary(long PathId, string TypeName, string? Name);

public sealed record InspectListResult(IReadOnlyList<InspectAssetSummary> Assets, int TotalCount);

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

public enum RepositoryRecoveryStatus
{
    Clean,
    RecoveryRequired,
    Recovered,
    Locked,
}

public sealed record RepositoryRecoveryOperation(string Kind, string InstallId, string Action);

public enum RepositoryRecoveryPlanAction
{
    RollBack,
    CompleteCleanup,
}

public enum RepositoryRecoveryFileAction
{
    NoChange,
    Restore,
    Delete,
}

public sealed record RepositoryRecoveryFileChange(string RelativePath, RepositoryRecoveryFileAction Action);

public sealed record RepositoryRecoveryPreview(
    RepositoryRecoveryStatus Status,
    string? GameDirectory,
    string? Kind,
    string? InstallId,
    RepositoryRecoveryPlanAction? Action,
    bool CanRecover,
    IReadOnlyList<RepositoryRecoveryFileChange> Files,
    IReadOnlyList<RepositoryRecoveryIssue> Issues);

public enum RepositoryRecoveryIssueCode
{
    RepositoryUnsafe,
    RecoveryUnsafe,
    OperationFailed,
    UnexpectedFailure,
}

public sealed record RepositoryRecoveryIssue(RepositoryRecoveryIssueCode Code, string Path)
{
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();
}

public sealed record RepositoryRecoveryReport(
    RepositoryRecoveryStatus Status,
    IReadOnlyList<RepositoryRecoveryOperation> Operations,
    IReadOnlyList<RepositoryRecoveryIssue> Issues)
{
    public static RepositoryRecoveryReport Clean { get; } = new(RepositoryRecoveryStatus.Clean, [], []);
}

public sealed class RepositoryRecoveryException : InvalidOperationException
{
    public RepositoryRecoveryReport Recovery { get; }

    public RepositoryRecoveryException(string message, RepositoryRecoveryReport recovery,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Recovery = recovery;
    }
}
