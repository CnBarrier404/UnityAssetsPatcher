using System.IO.Compression;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

internal sealed class WorkflowFactory
{
    private readonly ModManifestReader _manifestReader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;
    private readonly TargetAssetResolver _targetAssetResolver;

    public WorkflowFactory(
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive,
        TargetAssetResolver targetAssetResolver)
    {
        _manifestReader = manifestReader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _openPackageArchive = openPackageArchive;
        _targetAssetResolver = targetAssetResolver;
    }

    public InstallModWorkflow CreateInstallWorkflow(IAssetsAccessScope assets)
    {
        return new InstallModWorkflow(
            CreatePatchPlanBuilder(assets.Reader),
            new PatchOutputWriter(assets.Writer),
            assets,
            _manifestReader,
            _gameDirectoryResolver,
            _openPackageArchive,
            _targetAssetResolver);
    }

    public UninstallModWorkflow CreateUninstallWorkflow(string backupDirectory)
    {
        return new UninstallModWorkflow(new ModInstallationStore(backupDirectory));
    }

    private static PatchPlanBuilder CreatePatchPlanBuilder(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);

        return new PatchPlanBuilder(
            new FieldPatchPlanBuilder(assetQueryService),
            new ReplacementPlanBuilder(assetQueryService));
    }
}
