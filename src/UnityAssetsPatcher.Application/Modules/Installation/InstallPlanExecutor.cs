using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPlanExecutor
{
    private readonly InstallPatchApplier _patchApplier;
    private readonly InstallPayloadCopier _payloadCopier;
    private readonly InstallRecordBuilder _recordBuilder;

    public InstallPlanExecutor(
        InstallPatchApplier patchApplier,
        InstallPayloadCopier payloadCopier,
        InstallRecordBuilder recordBuilder)
    {
        _patchApplier = patchApplier;
        _payloadCopier = payloadCopier;
        _recordBuilder = recordBuilder;
    }

    public InstallExecutionResult Execute(
        InstallPlanSession session,
        string backupDirectory,
        StepTimer timings)
    {
        InstallPatchPlan patchWritePlan = session.Plan.PatchWritePlan
                                          ?? throw new InvalidOperationException(
                                              "Install plan does not contain a patch write plan.");
        var backupStore = new ModBackupStore(backupDirectory);
        InstallRecordPaths recordPaths = CreateRecordPaths(backupStore, session.Package);

        InstallPatchApplyResult? patchApplyResult = null;
        IReadOnlyList<InstallChange> copiedFiles = [];

        try
        {
            patchApplyResult = _patchApplier.Apply(
                patchWritePlan,
                recordPaths,
                timings);
            copiedFiles = _payloadCopier.Copy(session.Package, session.Plan.PayloadFiles, timings);
            InstallRecord record = _recordBuilder.Build(
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

    private static void RollbackInstall(
        InstallRecordPaths recordPaths,
        InstallPatchApplyResult? patchApplyResult,
        IReadOnlyList<InstallChange> copiedFiles)
    {
        InstallPayloadCopier.Rollback(copiedFiles);

        if (patchApplyResult is not null)
        {
            InstallPatchApplier.Rollback(patchApplyResult);
        }

        if (Directory.Exists(recordPaths.InstallDirectory))
        {
            Directory.Delete(recordPaths.InstallDirectory, true);
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
