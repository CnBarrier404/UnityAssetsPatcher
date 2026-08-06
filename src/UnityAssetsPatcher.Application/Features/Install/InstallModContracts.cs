using System.Text.Json.Serialization;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Features.Install;

public sealed record InstallRequest(string ZipFilePath, string? GameDirectory)
{
    public IReadOnlyList<string> SelectedOptionalGroups { get; init; } = [];
    public bool IncludePatchPreviewDetails { get; init; } = true;
    public PreparedInstall? PreparedInstall { get; init; }
}

public sealed record PreviewInstallRequest(InstallRequest Request) : IRequest<OperationResult<InstallPreviewResult>>;

public sealed record InstallModRequest(InstallRequest Request) : IRequest<OperationResult<InstallModResult>>;

public sealed record InstallModResult(
    string InstallId,
    string ModName,
    string ModVersion,
    IReadOnlyList<InstallChange> Changes,
    IReadOnlyList<string> OptionalGroups,
    TimingSnapshot Timing)
{
    public int BaseSnapshotCount { get; init; }
    public RepositoryRecoveryReport Recovery { get; init; } = RepositoryRecoveryReport.Clean;
}

public sealed record InstallPreviewResult(
    string ModName,
    string ModVersion,
    string ModAuthor,
    IReadOnlyList<InstallChange> Changes,
    IReadOnlyList<(string Name, string? Description)> OptionalGroups,
    TimingSnapshot Timing)
{
    [JsonIgnore]
    public PreparedInstall? PreparedInstall { get; init; }
}

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
