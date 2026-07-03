using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules.Installation;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly InstallPackageSource _packageSource;
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly InstallAssetsReadResources _assetsReadResources;
    private readonly InstallPayloadPlanner _payloadPlanner;
    private readonly InstallPayloadPreviewer _payloadPreviewer;
    private readonly InstallPayloadCopier _payloadCopier;
    private readonly InstallPatchPlanner _patchPlanner;
    private readonly InstallPatchApplier _patchApplier;
    private readonly InstallRecordBuilder _recordBuilder;
    private readonly InstallResultMapper _resultMapper;

    public InstallModWorkflow(
        InstallPackageSource packageSource,
        TargetAssetResolver targetAssetResolver,
        GameDirectoryResolver gameDirectoryResolver,
        InstallAssetsReadResources assetsReadResources,
        InstallPayloadPlanner payloadPlanner,
        InstallPayloadPreviewer payloadPreviewer,
        InstallPayloadCopier payloadCopier,
        InstallPatchPlanner patchPlanner,
        InstallPatchApplier patchApplier,
        InstallRecordBuilder recordBuilder,
        InstallResultMapper resultMapper)
    {
        _packageSource = packageSource;
        _targetAssetResolver = targetAssetResolver;
        _gameDirectoryResolver = gameDirectoryResolver;
        _assetsReadResources = assetsReadResources;
        _payloadPlanner = payloadPlanner;
        _payloadPreviewer = payloadPreviewer;
        _payloadCopier = payloadCopier;
        _patchPlanner = patchPlanner;
        _patchApplier = patchApplier;
        _recordBuilder = recordBuilder;
        _resultMapper = resultMapper;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new StepTimer();
        using ModPackage package = _packageSource.Open(request, timings);

        try
        {
            string gameDirectory =
                _gameDirectoryResolver.ResolveRequired(request.GameDirectory, package.Manifest.Info.Game);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadPlan = _payloadPlanner.Plan(package.Manifest, targets);
            InstallPatchPreview patchPreview = _patchPlanner.CreatePreview(targets, package, timings);
            var payloadPreview = _payloadPreviewer.Preview(payloadPlan);

            return _resultMapper.ToPreviewResult(package, patchPreview, payloadPreview, timings.BuildSnapshot());
        }
        finally
        {
            _assetsReadResources.Release();
        }
    }

    public InstallModResult Install(InstallModRequest request)
    {
        var timings = new StepTimer();
        using ModPackage package = _packageSource.Open(request, timings);

        try
        {
            string gameDirectory =
                _gameDirectoryResolver.ResolveRequired(request.GameDirectory, package.Manifest.Info.Game);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);

            var payloadPlan = _payloadPlanner.Plan(package.Manifest, targets);
            InstallPatchPlan patchPlan = _patchPlanner.CreateRequiredWritePlan(targets, package, timings);

            var backupStore = new ModBackupStore(request.BackupDirectory);
            InstallRecordPaths recordPaths = CreateRecordPaths(backupStore, package);

            InstallPatchApplyResult? patchApplyResult = null;
            IReadOnlyList<InstallChange> copiedFiles = [];

            try
            {
                patchApplyResult = _patchApplier.Apply(
                    patchPlan,
                    recordPaths,
                    timings);
                copiedFiles = _payloadCopier.Copy(package, payloadPlan, timings);
                InstallRecord record = _recordBuilder.Build(
                    package,
                    gameDirectory,
                    patchApplyResult,
                    copiedFiles,
                    package.AppliedOptionalGroups);

                backupStore.Save(record, recordPaths.InstallDirectory);

                return _resultMapper.ToInstallResult(
                    package,
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
