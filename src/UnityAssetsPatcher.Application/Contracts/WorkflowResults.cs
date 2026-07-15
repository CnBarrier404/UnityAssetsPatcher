using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InspectAssetSummary(long PathId, string TypeName, string? Name);

public sealed record InspectListResult(IReadOnlyList<InspectAssetSummary> Assets, int TotalCount);

public sealed record UninstallModResult(
    string InstallId,
    string ModName,
    string ModVersion,
    IReadOnlyList<UninstallRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallDeletedFileResult> DeletedFiles);

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

public sealed record PatchPreviewResult(IReadOnlyList<PatchPreviewAssetResult> Assets);

public sealed record PatchPreviewAssetResult(AssetsInfo Asset, IReadOnlyList<PatchPreviewOperationResult> Operations);

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
