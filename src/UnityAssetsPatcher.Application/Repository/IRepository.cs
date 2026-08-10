using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Uninstallation;

namespace UnityAssetsPatcher.Application.Repository;

public interface IRepository
{
    public void Initialize();

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods();

    public Task<RepositoryInstallResult> InstallModAsync(
        InstallModPlan plan,
        CancellationToken cancellationToken = default);

    public Task<UninstallModResult> UninstallModAsync(
        UninstallPlan plan,
        CancellationToken cancellationToken = default);
}
