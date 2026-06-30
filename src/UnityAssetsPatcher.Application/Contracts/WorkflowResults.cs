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
    string InstallBackupPath,
    string UninstallBackupPath);

public sealed record UninstallDeletedFileResult(string Source, string DestinationPath, bool Deleted);

public sealed record UninstallPreviewResult(
    string ModName,
    string ModVersion,
    string ModAuthor,
    bool CanUninstall,
    IReadOnlyList<UninstallPreviewRestoredFileResult> RestoredFiles,
    IReadOnlyList<UninstallPreviewDeletedFileResult> DeletedFiles);

public sealed record UninstallPreviewRestoredFileResult(
    string Target,
    string AssetsFilePath,
    string InstallBackupPath,
    bool TargetExists,
    bool BackupExists);

public sealed record UninstallPreviewDeletedFileResult(string Source, string DestinationPath, bool Exists);

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
    string GameDirectory,
    DateTimeOffset InstalledAt);
