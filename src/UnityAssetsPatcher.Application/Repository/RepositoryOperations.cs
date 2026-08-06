using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Installation;

namespace UnityAssetsPatcher.Application.Repository;

public sealed record InstallModPlan(
    string PackagePath,
    InstallAnalysis Analysis,
    IReadOnlyList<PreparedInstallAssetFile>? ExpectedAssetFiles = null);

public sealed record RepositoryInstallResult(
    InstallExecutionResult Execution,
    RepositoryRecoveryReport Recovery,
    TimingSnapshot Timing);
