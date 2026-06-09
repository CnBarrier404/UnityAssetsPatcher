using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PatchAssetApplier
{
    private readonly PatchOutputWriter _patchOutputWriter;

    public PatchAssetApplier(PatchOutputWriter patchOutputWriter)
    {
        _patchOutputWriter = patchOutputWriter;
    }

    public PatchAssetApplyResult Execute(
        PatchAssetPlan plan,
        string backupDirectory,
        WorkflowTiming timings)
    {
        var files = timings.MeasureApplyPatches(() => plan.Files
            .Select(file =>
            {
                PatchApplyResult result = _patchOutputWriter.Write(
                    file.AssetsFilePath,
                    null,
                    backupDirectory,
                    file.PatchPlan);

                return new { File = file, Result = result };
            })
            .Where(item => item.Result.OperationCount != 0)
            .Select(item =>
            {
                string backupPath = item.Result.BackupPath ??
                                    throw new InvalidOperationException("Patch write did not create a backup.");

                return new PatchAssetAppliedFile(
                    item.File.Target,
                    item.Result.OutputPath,
                    backupPath,
                    item.Result.AssetCount,
                    item.Result.OperationCount);
            })
            .ToArray());

        return new PatchAssetApplyResult(files);
    }
}

public sealed record PatchAssetApplyResult(IReadOnlyList<PatchAssetAppliedFile> Files);

public sealed record PatchAssetAppliedFile(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);
