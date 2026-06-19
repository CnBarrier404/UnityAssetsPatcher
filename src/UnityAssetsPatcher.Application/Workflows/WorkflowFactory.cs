using System.IO.Compression;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowFactory
{
    private readonly IModManifestLoader _manifestLoader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;

    public WorkflowFactory() : this(new ModManifestLoader(),
        new GameDirectoryResolver()) { }

    public WorkflowFactory(IModManifestLoader manifestLoader) : this(
        manifestLoader,
        new GameDirectoryResolver()) { }

    public WorkflowFactory(
        IModManifestLoader manifestLoader,
        IEnumerable<string> steamRoots) : this(
        manifestLoader,
        new GameDirectoryResolver(steamRoots)) { }

    public WorkflowFactory(
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver)
        : this(manifestLoader, gameDirectoryResolver, PackageArchive.OpenRead) { }

    public WorkflowFactory(
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive)
    {
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _openPackageArchive = openPackageArchive;
    }

    public InstallModWorkflow CreateInstallModWorkflow(IAssetsAccessScope assets)
    {
        PatchPlanBuilder patchPlanBuilder = CreatePatchPlanBuilder(assets.Reader);
        var patchOutputWriter = new PatchOutputWriter(assets.Writer);
        var patchAssetsWorkflow = new PatchAssetsWorkflow(patchPlanBuilder, patchOutputWriter);

        return new InstallModWorkflow(
            patchAssetsWorkflow,
            assets,
            _manifestLoader,
            _gameDirectoryResolver,
            _openPackageArchive);
    }

    public UninstallModWorkflow CreateUninstallModWorkflow(string backupDirectory)
    {
        return new UninstallModWorkflow(new ModInstallationStore(backupDirectory));
    }

    public FindAssetsWorkflow CreateFindAssetsWorkflow(IAssetsAccessScope assets)
    {
        return new FindAssetsWorkflow(
            new AssetQueryService(assets.Reader),
            _manifestLoader,
            new ManifestTargetSelector());
    }

    private static PatchPlanBuilder CreatePatchPlanBuilder(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);
        var fieldPatchPlanBuilder = new FieldPatchPlanBuilder(assetQueryService);
        var replacementPlanBuilder = new ReplacementPlanBuilder(assetQueryService);

        return new PatchPlanBuilder(fieldPatchPlanBuilder, replacementPlanBuilder);
    }
}
