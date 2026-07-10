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
            plan.GameDirectory,
            plan.CanUninstall,
            plan.BlockingRecords.Select(blocker => new UninstallBlockingModResult(
                blocker.Entry.Record.ModName,
                blocker.Entry.Record.ModVersion,
                blocker.Entry.Record.InstalledAt,
                blocker.OverlappingAssetsFiles)).ToArray(),
            plan.RestoredFiles,
            plan.DeletedFiles);
    }

    public static UninstallModResult ToUninstallResult(InstallRecord record, UninstallExecutionResult execution)
    {
        return new UninstallModResult(
            record.ModName,
            record.ModVersion,
            record.ModAuthor,
            execution.RestoredFiles,
            execution.DeletedFiles);
    }
}
