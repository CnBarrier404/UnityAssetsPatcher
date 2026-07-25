using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Backups;

public interface IBackupService
{
    public IReadOnlyList<InstallRecordSummary> ListInstalledMods();
    public BackupRecoveryReport CheckRecovery();
    public BackupRecoveryPreview PreviewRecovery(string gameDirectory);
    public BackupRecoveryReport Recover(string gameDirectory);
}
