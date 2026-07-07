using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules.Installation;
using UnityAssetsPatcher.Application.Modules.Patching;
using UnityAssetsPatcher.Application.Modules.Uninstallation;
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
        var planner = new InstallPlanner(
            _manifestReader,
            _targetAssetResolver,
            _gameDirectoryResolver,
            patchPlanBuilder);

        var executor = new InstallExecutor(new PatchOutputWriter(assets.Writer), assets);

        return new InstallModWorkflow(
            planner,
            executor);
    }

    public UninstallModWorkflow CreateUninstallWorkflow(string backupDirectory)
    {
        var backupStore = new ModBackupStore(backupDirectory);

        return new UninstallModWorkflow(
            new UninstallPlanner(backupStore),
            new UninstallExecutor());
    }

    private static PatchPlanBuilder CreatePatchPlanBuilder(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);

        return new PatchPlanBuilder(
            new FieldPatchPlanBuilder(assetQueryService),
            new ReplacementPlanBuilder(assetQueryService));
    }
}
