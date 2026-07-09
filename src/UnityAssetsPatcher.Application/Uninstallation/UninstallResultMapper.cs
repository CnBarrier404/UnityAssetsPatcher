using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Uninstallation;

public static class UninstallResultMapper
{
    public static UninstallPreviewResult ToPreviewResult(UninstallPreviewPlan plan)
    {
        return new UninstallPreviewResult(
            plan.Record.ModName,
            plan.Record.ModVersion,
            plan.Record.ModAuthor,
            plan.CanUninstall,
            plan.RestoredFiles,
            plan.DeletedFiles);
    }

    public static UninstallModResult ToUninstallResult(
        InstallRecord record,
        UninstallExecutionResult execution)
    {
        return new UninstallModResult(
            record.ModName,
            record.ModVersion,
            record.ModAuthor,
            execution.RestoredFiles,
            execution.DeletedFiles);
    }
}
