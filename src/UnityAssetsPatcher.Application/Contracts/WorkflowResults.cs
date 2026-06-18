using System.Text.Json;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InstallModResult(
    string ModName,
    string ModVersion,
    string ModAuthor,
    IReadOnlyList<InstallModFileResult> Files,
    IReadOnlyList<InstallCopiedFileResult> CopiedFiles,
    InstallTimingResult Timing);

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
    InstallTimingResult Timing);

public sealed record InstallTimingResult(
    TimeSpan ReadPackage,
    TimeSpan PrepareSources,
    TimeSpan FindGameFiles,
    TimeSpan AnalyzeChanges,
    TimeSpan? ApplyPatches,
    TimeSpan? CopyFiles,
    TimeSpan Elapsed);

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

public sealed record InspectListResult(IReadOnlyList<AssetsInfo> Assets, int TotalCount);

public sealed record AssetMatch(AssetsInfo Asset, IReadOnlyDictionary<string, JsonElement> IncludeGroup);

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
