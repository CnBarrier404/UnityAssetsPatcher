using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Repository;

public interface IRepositoryService
{
    public IReadOnlyList<InstallRecordSummary> ListInstalledMods();
    public RepositoryRecoveryReport CheckRecovery();
    public RepositoryRecoveryPreview PreviewRecovery(string gameDirectory);
    public RepositoryRecoveryReport Recover(string gameDirectory);
}
