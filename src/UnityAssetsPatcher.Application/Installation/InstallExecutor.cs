using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Installation;

public sealed class InstallExecutor
{
    private readonly PatchOutputWriter _patchOutputWriter;
    private readonly IAssetsAccessScope _assets;

    public InstallExecutor(PatchOutputWriter patchOutputWriter, IAssetsAccessScope assets)
    {
        _patchOutputWriter = patchOutputWriter;
        _assets = assets;
    }

    public void ReleaseReadResources()
    {
        _assets.ReleaseReadResources();
    }

    public InstallExecutionResult Execute(
        InstallPlanSession session,
        string backupDirectory,
        StepTimer timings)
    {
        InstallWritePlan writePlan = session.Plan.Write
                                     ?? throw new InvalidOperationException(
                                         "Install plan does not contain a write plan.");
        var backupStore = new ModBackupStore(backupDirectory);
        InstallRecordPaths recordPaths = CreateRecordPaths(backupStore, session.Package);

        InstallPatchApplyResult? patchApplyResult = null;
        IReadOnlyList<InstallChange> copiedFiles = [];

        try
        {
            patchApplyResult = ApplyPatches(writePlan.Patch, recordPaths, timings);
            copiedFiles = CopyPayloadFiles(session.Package, writePlan.PayloadFiles, timings);
            InstallRecord record = BuildRecord(
                session.Package,
                session.Plan.GameDirectory,
                patchApplyResult,
                copiedFiles,
                session.Package.AppliedOptionalGroups);

            backupStore.Save(record, recordPaths.InstallDirectory);

            return new InstallExecutionResult(patchApplyResult, copiedFiles);
        }
        catch (Exception ex)
        {
            try
            {
                RollbackInstall(recordPaths, patchApplyResult, copiedFiles);
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Install failed and rollback also failed.", new AggregateException(ex, rollbackException));
            }

            throw;
        }
    }

    private InstallPatchApplyResult ApplyPatches(
        InstallPatchPlan patchPlan,
        InstallRecordPaths recordPaths,
        StepTimer timings)
    {
        ReleaseReadResources();

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
                RollbackPatches(new InstallPatchApplyResult(appliedFiles));
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

    private static IReadOnlyList<InstallChange> CopyPayloadFiles(
        ModPackage package,
        IReadOnlyList<InstallPayloadFilePlan> files,
        StepTimer timings)
    {
        return timings.Measure("copy-files", () =>
        {
            if (files.Count == 0)
            {
                return [];
            }

            var results = new List<InstallChange>();

            try
            {
                foreach (InstallPayloadFilePlan file in files)
                {
                    try
                    {
                        package.CopyPayloadFile(file.Source, file.DestinationPath);
                    }
                    catch (IOException ex) when (File.Exists(file.DestinationPath))
                    {
                        throw new IOException(
                            $"Payload file was created by another process during installation: {file.DestinationPath}",
                            ex);
                    }

                    results.Add(new InstallChange(InstallChangeKind.Payload, file.Source, file.DestinationPath));
                }

                return results.ToArray();
            }
            catch (Exception ex) when (results.Count > 0)
            {
                try
                {
                    RollbackPayloadFiles(results);
                }
                catch (Exception rollbackException)
                {
                    throw new InvalidOperationException(
                        "Payload copy failed and rollback also failed.",
                        new AggregateException(ex, rollbackException));
                }

                throw;
            }
        });
    }

    private static InstallRecord BuildRecord(
        ModPackage package,
        string gameDirectory,
        InstallPatchApplyResult patchApplyResult,
        IReadOnlyList<InstallChange> copiedFiles,
        IReadOnlyList<string> appliedOptionalGroups)
    {
        return new InstallRecord(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.Now,
            package.Manifest.Info.Name,
            package.Manifest.Info.Version,
            package.Manifest.Info.Author,
            package.PackagePath,
            gameDirectory,
            patchApplyResult.Files
                .Select(file => new InstallRecordPatchedFile(
                    file.Target,
                    file.AssetsFilePath,
                    file.BackupPath,
                    file.AssetCount,
                    file.OperationCount))
                .ToArray(),
            copiedFiles
                .Where(file => file.Kind == InstallChangeKind.Payload)
                .Select(file => new InstallRecordCopiedFile(
                    file.Name,
                    file.Path,
                    File.Exists(file.Path)))
                .ToArray())
        {
            OptionalGroups = appliedOptionalGroups.Count == 0 ? null : appliedOptionalGroups,
        };
    }

    private static void RollbackInstall(
        InstallRecordPaths recordPaths,
        InstallPatchApplyResult? patchApplyResult,
        IReadOnlyList<InstallChange> copiedFiles)
    {
        RollbackPayloadFiles(copiedFiles);

        if (patchApplyResult is not null)
        {
            RollbackPatches(patchApplyResult);
        }

        if (Directory.Exists(recordPaths.InstallDirectory))
        {
            Directory.Delete(recordPaths.InstallDirectory, true);
        }
    }

    private static void RollbackPatches(InstallPatchApplyResult result)
    {
        foreach (InstallPatchAppliedFile file in result.Files.Reverse())
        {
            if (!File.Exists(file.BackupPath))
            {
                throw new FileNotFoundException(
                    $"Install rollback backup not found: {file.BackupPath}",
                    file.BackupPath);
            }

            ModBackupStore.RestoreFile(file.BackupPath, file.AssetsFilePath);
        }
    }

    private static void RollbackPayloadFiles(IReadOnlyList<InstallChange> copiedFiles)
    {
        foreach (InstallChange file in copiedFiles.Reverse())
        {
            if (file.Kind != InstallChangeKind.Payload)
            {
                continue;
            }

            if (File.Exists(file.Path))
            {
                File.Delete(file.Path);
            }
        }
    }

    private static InstallRecordPaths CreateRecordPaths(ModBackupStore backupStore, ModPackage package)
    {
        string installDirectory = backupStore.CreateInstallDirectory(
            package.Manifest.Info.Name,
            package.Manifest.Info.Version);

        return new InstallRecordPaths(installDirectory, Path.Combine(installDirectory, "assets"));
    }
}

public sealed record InstallExecutionResult(
    InstallPatchApplyResult PatchApplyResult,
    IReadOnlyList<InstallChange> CopiedFiles);

public sealed record InstallPatchApplyResult(IReadOnlyList<InstallPatchAppliedFile> Files);

public sealed record InstallPatchAppliedFile(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);

public sealed record InstallRecordPaths(string InstallDirectory, string AssetsBackupDirectory);
