using System.IO.Compression;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowFactory
{
    private readonly ModManifestReader _manifestReader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;

    public WorkflowFactory() : this(new ModManifestReader(),
        new GameDirectoryResolver()) { }

    public WorkflowFactory(
        ModManifestReader manifestReader,
        IEnumerable<string> steamRoots) : this(
        manifestReader,
        new GameDirectoryResolver(steamRoots)) { }

    public WorkflowFactory(
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver)
        : this(manifestReader, gameDirectoryResolver, PackageArchive.OpenRead) { }

    public WorkflowFactory(
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive)
    {
        _manifestReader = manifestReader;
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
            _manifestReader,
            _gameDirectoryResolver,
            _openPackageArchive);
    }

    public UninstallModWorkflow CreateUninstallModWorkflow(string backupDirectory)
    {
        return new UninstallModWorkflow(new ModInstallationStore(backupDirectory));
    }

    private static PatchPlanBuilder CreatePatchPlanBuilder(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);
        var fieldPatchPlanBuilder = new FieldPatchPlanBuilder(assetQueryService);
        var replacementPlanBuilder = new ReplacementPlanBuilder(assetQueryService);

        return new PatchPlanBuilder(fieldPatchPlanBuilder, replacementPlanBuilder);
    }
}
