using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules.Patching;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPatchApplier
{
    private readonly PatchOutputWriter _patchOutputWriter;
    private readonly InstallAssetsReadResources _assetsReadResources;

    public InstallPatchApplier(
        PatchOutputWriter patchOutputWriter,
        InstallAssetsReadResources assetsReadResources)
    {
        _patchOutputWriter = patchOutputWriter;
        _assetsReadResources = assetsReadResources;
    }

    public InstallPatchApplyResult Apply(InstallPatchPlan patchPlan, InstallRecordPaths recordPaths, StepTimer timings)
    {
        _assetsReadResources.Release();

        var files = timings.Measure("apply-patches", () => patchPlan.Files
            .Select(file =>
            {
                var result = _patchOutputWriter.Write(
                    file.AssetsFilePath,
                    null,
                    recordPaths.AssetsBackupDirectory,
                    file.PatchPlan);

                return new InstallPatchWriteResult(file, result);
            })
            .Where(item => item.Result.OperationCount != 0)
            .Select(item =>
            {
                string backupPath = item.Result.BackupPath ??
                                    throw new InvalidOperationException("Patch write did not create a backup.");

                return new InstallPatchAppliedFile(
                    item.File.Target,
                    item.Result.OutputPath,
                    backupPath,
                    item.Result.AssetCount,
                    item.Result.OperationCount);
            })
            .ToArray());

        return new InstallPatchApplyResult(files);
    }

    private sealed record InstallPatchWriteResult(InstallPatchPlanFile File, PatchApplyResult Result);
}
