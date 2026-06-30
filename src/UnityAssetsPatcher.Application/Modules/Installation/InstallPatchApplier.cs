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

        var appliedFiles = new List<InstallPatchAppliedFile>();

        try
        {
            var files = timings.Measure("apply-patches", () =>
            {
                appliedFiles.AddRange(from file in patchPlan.Files
                    let result =
                        _patchOutputWriter.Write(file.AssetsFilePath, null, recordPaths.AssetsBackupDirectory,
                            file.PatchPlan)
                    where result.OperationCount != 0
                    let backupPath =
                        result.BackupPath ?? throw new InvalidOperationException("Patch write did not create a backup.")
                    select new InstallPatchAppliedFile(file.Target, result.OutputPath, backupPath, result.AssetCount,
                        result.OperationCount));

                return appliedFiles.ToArray();
            });

            return new InstallPatchApplyResult(files);
        }
        catch (Exception ex) when (appliedFiles.Count > 0)
        {
            try
            {
                Rollback(new InstallPatchApplyResult(appliedFiles));
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Patch application failed and rollback also failed.",
                    new AggregateException(ex, rollbackException));
            }

            throw;
        }
    }

    public static void Rollback(InstallPatchApplyResult result)
    {
        foreach (InstallPatchAppliedFile file in result.Files.Reverse())
        {
            RestoreBackup(file.BackupPath, file.AssetsFilePath);
        }
    }

    private static void RestoreBackup(string backupPath, string assetsFilePath)
    {
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Install rollback backup not found: {backupPath}", backupPath);
        }

        string? directory = Path.GetDirectoryName(assetsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = Path.Combine(
            directory ?? Directory.GetCurrentDirectory(),
            $".{Path.GetFileName(assetsFilePath)}.{Guid.NewGuid():N}.rollback.tmp");

        try
        {
            File.Copy(backupPath, temporaryPath, true);
            File.Move(temporaryPath, assetsFilePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
