using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Uninstallation;

namespace UnityAssetsPatcher.Application.Repository;

public interface IRepository
{
    public void Initialize();

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods();

    public RepositoryInstallResult InstallMod(InstallModPlan plan);

    public UninstallModResult UninstallMod(UninstallPlan plan);
}
