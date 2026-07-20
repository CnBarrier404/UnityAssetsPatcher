using UnityAssetsPatcher.Application.Installation;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InstallRequest(string ZipFilePath, string? GameDirectory)
{
    public IReadOnlyList<string> SelectedOptionalGroups { get; init; } = [];
    public bool IncludePatchPreviewDetails { get; init; } = true;
}

public sealed record InstallModResult(
    string InstallId,
    string ModName,
    string ModVersion,
    IReadOnlyList<InstallChange> Changes,
    IReadOnlyList<string> OptionalGroups,
    TimingSnapshot Timing)
{
    public BackupRecoveryReport Recovery { get; init; } = BackupRecoveryReport.Clean;
}

public sealed record InstallPreviewResult(
    string ModName,
    string ModVersion,
    string ModAuthor,
    IReadOnlyList<InstallChange> Changes,
    IReadOnlyList<(string Name, string? Description)> OptionalGroups,
    TimingSnapshot Timing);

public enum InstallChangeKind
{
    Patch,
    Payload,
}

public sealed record InstallChange(
    InstallChangeKind Kind,
    string Name,
    string Path,
    PatchPreviewResult? Preview = null,
    string? BackupPath = null,
    int AssetCount = 0,
    int OperationCount = 0);
