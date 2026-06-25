using System.Text.Json;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InstallModResult(
    string ModName,
    string ModVersion,
    string ModAuthor,
    IReadOnlyList<InstallModFileResult> Files,
    IReadOnlyList<InstallCopiedFileResult> CopiedFiles,
    IReadOnlyList<string> OptionalGroups,
    TimingSnapshot Timing);

public sealed record InstallModFileResult(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);

public sealed record InstallCopiedFileResult(string Source, string DestinationPath);

public sealed record InstallPreviewResult(
    string ModName,
    string ModVersion,
    string ModAuthor,
    IReadOnlyList<InstallPreviewFileResult> Files,
    IReadOnlyList<InstallCopyFilePreviewResult> CopiedFiles,
    IReadOnlyList<OptionalGroupPreview> OptionalGroups,
    TimingSnapshot Timing);

public sealed record OptionalGroupPreview(string Name, string? Description);

public sealed record InstallPreviewFileResult(
    string Target,
    string AssetsFilePath,
    PatchPreviewResult Preview);

public sealed record InstallCopyFilePreviewResult(string Source, string DestinationPath, bool WillCopy);

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

public sealed record PatchAssetPreview(IReadOnlyList<PatchAssetPreviewFile> Files);

public sealed record PatchAssetPreviewFile(string Target, string AssetsFilePath, PatchPreviewResult Preview);

public sealed record PatchAssetPlan(IReadOnlyList<PatchAssetFilePlan> Files);

public sealed record PatchAssetFilePlan(string Target, string AssetsFilePath, PatchFileWritePlan PatchPlan);

public sealed record PatchAssetApplyResult(IReadOnlyList<PatchAssetAppliedFile> Files);

public sealed record PatchAssetAppliedFile(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);
