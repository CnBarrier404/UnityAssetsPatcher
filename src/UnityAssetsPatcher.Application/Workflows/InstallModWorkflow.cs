using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules.Installation;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly InstallPlanBuilder _planBuilder;
    private readonly InstallAssetsReadResources _assetsReadResources;
    private readonly InstallPayloadPreviewer _payloadPreviewer;
    private readonly InstallPayloadCopier _payloadCopier;
    private readonly InstallPatchApplier _patchApplier;
    private readonly InstallRecordBuilder _recordBuilder;
    private readonly InstallResultMapper _resultMapper;

    public InstallModWorkflow(
        InstallPlanBuilder planBuilder,
        InstallAssetsReadResources assetsReadResources,
        InstallPayloadPreviewer payloadPreviewer,
        InstallPayloadCopier payloadCopier,
        InstallPatchApplier patchApplier,
        InstallRecordBuilder recordBuilder,
        InstallResultMapper resultMapper)
    {
        _planBuilder = planBuilder;
        _assetsReadResources = assetsReadResources;
        _payloadPreviewer = payloadPreviewer;
        _payloadCopier = payloadCopier;
        _patchApplier = patchApplier;
        _recordBuilder = recordBuilder;
        _resultMapper = resultMapper;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new StepTimer();

        try
        {
            using InstallPlanSession session = _planBuilder.BuildPreview(request, timings);
            InstallPatchPreview patchPreview = session.Plan.PatchPreview
                                               ?? throw new InvalidOperationException(
                                                   "Preview plan does not contain a patch preview.");
            var payloadPreview = _payloadPreviewer.Preview(session.Plan.PayloadFiles);

            return _resultMapper.ToPreviewResult(
                session.Package,
                patchPreview,
                payloadPreview,
                timings.BuildSnapshot());
        }
        finally
        {
            _assetsReadResources.Release();
        }
    }

    public InstallModResult Install(InstallModRequest request)
    {
        var timings = new StepTimer();

        try
        {
            using InstallPlanSession session = _planBuilder.BuildInstall(request, timings);
            InstallPatchPlan patchWritePlan = session.Plan.PatchWritePlan
                                              ?? throw new InvalidOperationException(
                                                  "Install plan does not contain a patch write plan.");

            var backupStore = new ModBackupStore(request.BackupDirectory);
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

                return _resultMapper.ToInstallResult(
                    session.Package,
                    patchApplyResult,
                    copiedFiles,
                    timings.BuildSnapshot());
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
                        "Install failed and rollback also failed.",
                        new AggregateException(ex, rollbackException));
                }

                throw;
            }
        }
        finally
        {
            _assetsReadResources.Release();
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

        return new InstallRecordPaths(
            installDirectory,
            Path.Combine(installDirectory, "assets"));
    }
}
