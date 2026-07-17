using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

internal sealed class WorkflowFactory
{
    private readonly ModManifestReader _manifestReader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly BackupRepository _backupRepository;

    public WorkflowFactory(
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        TargetAssetResolver targetAssetResolver,
        BackupRepository backupRepository)
    {
        _manifestReader = manifestReader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _targetAssetResolver = targetAssetResolver;
        _backupRepository = backupRepository;
    }

    public InstallModWorkflow CreateInstallWorkflow(IAssetsAccessScope assets)
    {
        PatchPlanBuilder patchPlanBuilder = CreatePatchPlanBuilder(assets.Reader);
        var planner = new InstallPlanner(
            _manifestReader,
            _targetAssetResolver,
            _gameDirectoryResolver,
            patchPlanBuilder);

        var executor = new InstallExecutor(new PatchOutputWriter(assets.Writer), assets, _backupRepository);

        return new InstallModWorkflow(planner, executor, _backupRepository);
    }

    public UninstallModWorkflow CreateUninstallWorkflow()
    {
        return new UninstallModWorkflow(
            new UninstallPlanner(_backupRepository, _gameDirectoryResolver),
            new UninstallExecutor(_backupRepository),
            _backupRepository);
    }

    public InspectAssetsWorkflow CreateInspectWorkflow(IAssetsAccessScope assets)
    {
        return new InspectAssetsWorkflow(assets.Reader);
    }

    private static PatchPlanBuilder CreatePatchPlanBuilder(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);

        return new PatchPlanBuilder(
            new FieldPatchPlanBuilder(assetQueryService),
            new ReplacementPlanBuilder(assetQueryService));
    }
}
