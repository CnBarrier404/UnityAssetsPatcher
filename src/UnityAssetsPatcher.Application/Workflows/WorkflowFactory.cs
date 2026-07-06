using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules.Installation;
using UnityAssetsPatcher.Application.Modules.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

internal sealed class WorkflowFactory
{
    private readonly ModManifestReader _manifestReader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly TargetAssetResolver _targetAssetResolver;

    public WorkflowFactory(
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        TargetAssetResolver targetAssetResolver)
    {
        _manifestReader = manifestReader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _targetAssetResolver = targetAssetResolver;
    }

    public InstallModWorkflow CreateInstallWorkflow(IAssetsAccessScope assets)
    {
        PatchPlanBuilder patchPlanBuilder = CreatePatchPlanBuilder(assets.Reader);
        var assetsReadResources = new InstallAssetsReadResources(assets);
        var planBuilder = new InstallPlanBuilder(
            new InstallPackageSource(_manifestReader),
            _targetAssetResolver,
            _gameDirectoryResolver,
            new InstallPayloadPlanner(),
            new InstallPatchPlanner(patchPlanBuilder));

        return new InstallModWorkflow(
            planBuilder,
            assetsReadResources,
            new InstallPayloadPreviewer(),
            new InstallPayloadCopier(),
            new InstallPatchApplier(new PatchOutputWriter(assets.Writer), assetsReadResources),
            new InstallRecordBuilder(),
            new InstallResultMapper());
    }

    public UninstallModWorkflow CreateUninstallWorkflow(string backupDirectory)
    {
        return new UninstallModWorkflow(new ModBackupStore(backupDirectory));
    }

    private static PatchPlanBuilder CreatePatchPlanBuilder(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);

        return new PatchPlanBuilder(
            new FieldPatchPlanBuilder(assetQueryService),
            new ReplacementPlanBuilder(assetQueryService));
    }
}
