using System.Text.Json;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record UninstallModResult(
    string ModName,
    string ModVersion,
    string ModAuthor,
    IReadOnlyList<UninstallRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallDeletedFileResult> DeletedFiles);

public sealed record UninstallRestoredFileResult(
    string Target,
    string AssetsFilePath,
    string InstallBackupPath);

public sealed record UninstallDeletedFileResult(string Source, string DestinationPath, bool Deleted);

public sealed record UninstallPreviewResult(
    string ModName,
    string ModVersion,
    string ModAuthor,
    string GameDirectory,
    bool CanUninstall,
    IReadOnlyList<UninstallBlockingModResult> BlockingMods,
    IReadOnlyList<UninstallPreviewRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallPreviewDeletedFileResult> DeletedFiles);

public sealed record UninstallPreviewRestoredFileResult(
    string Target,
    string AssetsFilePath,
    string InstallBackupPath,
    FileIntegrityStatus TargetStatus,
    FileIntegrityStatus BackupStatus);

public sealed record UninstallPreviewDeletedFileResult(
    string Source,
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
    JsonElement From,
    JsonElement To,
    string FromText,
    string ToText,
    bool WillChange);

public sealed record InstallRecordSummary(
    string InstallDirectory,
    string ModName,
    string ModVersion,
    string? GameName,
    DateTimeOffset InstalledAt);
