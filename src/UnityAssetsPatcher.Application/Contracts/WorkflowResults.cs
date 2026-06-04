using System.Text.Json;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record PatchApplyResult(string OutputPath, string? BackupPath, int AssetCount, int OperationCount);

public sealed record InstallModResult(
    string ModName,
    string ModVersion,
    IReadOnlyList<InstallModFileResult> Files,
    IReadOnlyList<InstallCopiedFileResult> CopiedFiles);

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
    IReadOnlyList<InstallPreviewFileResult> Files,
    IReadOnlyList<InstallCopyFilePreviewResult> CopiedFiles);

public sealed record InstallPreviewFileResult(
    string Target,
    string AssetsFilePath,
    PatchPreviewResult Preview);

public sealed record InstallCopyFilePreviewResult(string Source, string DestinationPath, bool WillCopy);

public sealed record AssetMatch(AssetsInfo Asset, IReadOnlyDictionary<string, JsonElement> IncludeGroup);

public sealed record PatchPreviewResult(IReadOnlyList<PatchPreviewAssetResult> Assets);

public sealed record PatchPreviewAssetResult(AssetsInfo Asset, IReadOnlyList<PatchPreviewOperationResult> Operations);

public sealed record PatchPreviewOperationResult(
    string Path,
    string OldValue,
    JsonElement From,
    JsonElement To,
    bool WillChange);
