using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Manifests;
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
        GameDirectoryResolver gameDirectoryResolver)
    {
        _assetsPatchWriter = assetsPatchWriter;
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
    }

    public InstallModWorkflow CreateInstallModWorkflow(IAssetsFileReader assetsReader)
    {
        return new InstallModWorkflow(CreatePatchAssetsWorkflow(assetsReader), _manifestLoader, _gameDirectoryResolver);
    }

    private PatchAssetsWorkflow CreatePatchAssetsWorkflow(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);
        var valueResolver = new PatchValueResolver(assetQueryService);
        var fieldPatchPlanBuilder = new FieldPatchPlanBuilder(assetQueryService, valueResolver);
        var replacementPlanBuilder = new ReplacementPlanBuilder(assetQueryService);
        var patchPlanBuilder = new PatchPlanBuilder(fieldPatchPlanBuilder, replacementPlanBuilder);
        var patchOutputWriter = new PatchOutputWriter(_assetsPatchWriter);
        Action releaseReadResources = assetsReader is IDisposable disposable ? disposable.Dispose : static () => { };

        return new PatchAssetsWorkflow(
            patchPlanBuilder,
            patchOutputWriter,
            releaseReadResources);
    }
}
