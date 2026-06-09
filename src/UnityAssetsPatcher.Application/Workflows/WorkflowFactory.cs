using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowFactory
{
    private readonly IAssetsFileWriter _assetsPatchWriter;
    private readonly IModManifestLoader _manifestLoader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;

    public WorkflowFactory(IAssetsFileWriter assetsPatchWriter) : this(assetsPatchWriter, new ModManifestLoader(),
        new GameDirectoryResolver()) { }

    public WorkflowFactory(
        IAssetsFileWriter assetsPatchWriter,
        IModManifestLoader manifestLoader,
        IEnumerable<string> steamRoots) : this(
        assetsPatchWriter,
        manifestLoader,
        new GameDirectoryResolver(steamRoots)) { }

    public WorkflowFactory(
        IAssetsFileWriter assetsPatchWriter,
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver)
    {
        _assetsPatchWriter = assetsPatchWriter;
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
    }

    public InstallModWorkflow CreateInstallModWorkflow(IAssetsFileReader assetsReader)
    {
        PatchPlanBuilder patchPlanBuilder = CreatePatchPlanBuilder(assetsReader);
        var patchOutputWriter = new PatchOutputWriter(_assetsPatchWriter);
        Action releaseReadResources = assetsReader is IDisposable disposable ? disposable.Dispose : static () => { };

        return new InstallModWorkflow(
            patchPlanBuilder,
            patchOutputWriter,
            releaseReadResources,
            _manifestLoader,
            _gameDirectoryResolver);
    }

    private static PatchPlanBuilder CreatePatchPlanBuilder(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);
        var fieldPatchPlanBuilder = new FieldPatchPlanBuilder(assetQueryService);
        var replacementPlanBuilder = new ReplacementPlanBuilder(assetQueryService);

        return new PatchPlanBuilder(fieldPatchPlanBuilder, replacementPlanBuilder);
    }
}
