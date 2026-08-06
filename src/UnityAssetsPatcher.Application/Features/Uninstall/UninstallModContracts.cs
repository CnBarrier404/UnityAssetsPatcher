using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Features.Uninstall;

public sealed record UninstallPreviewRequest(string InstallId, string? GameDirectory = null) :
    IRequest<OperationResult<UninstallPreviewResult>>;

public sealed record UninstallModRequest(string InstallId, string? GameDirectory = null) :
    IRequest<OperationResult<UninstallModResult>>;

public sealed record UninstallModResult(
    string InstallId,
    string ModName,
    string ModVersion,
    IReadOnlyList<UninstallChangedFileResult> ChangedFiles)
{
    public RepositoryRecoveryReport Recovery { get; init; } = RepositoryRecoveryReport.Clean;
}

public sealed record UninstallChangedFileResult(
    string RelativePath,
    UninstallChangedFileAction Action,
    FileIntegrityStatus Status);

public enum UninstallChangedFileAction
{
    Rebuild,
    RestoreBase,
    Delete,
}

public sealed record UninstallPreviewResult(
    string InstallId,
    string ModName,
    string ModVersion,
    DateTimeOffset InstalledAt,
    string GameDirectory,
    bool CanUninstall,
    IReadOnlyList<UninstallDependencyFailureResult> DependencyFailures,
    IReadOnlyList<UninstallChangedFileResult> ChangedFiles);

public sealed record UninstallDependencyFailureResult(
    string ModName,
    string ModVersion,
    string RelativePath,
    PatchDiagnostic Diagnostic);

public enum FileIntegrityStatus
{
    Matches,
    Missing,
    Modified,
    Unreadable,
}
