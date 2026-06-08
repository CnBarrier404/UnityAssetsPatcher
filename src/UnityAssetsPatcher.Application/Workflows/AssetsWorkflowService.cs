using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class AssetsWorkflowService
{
    private readonly InstallModWorkflow _installModWorkflow;

    public AssetsWorkflowService(IAssetsFileService assetsFileService)
        : this(assetsFileService, assetsFileService, new ModManifestLoader()) { }

    public AssetsWorkflowService(IAssetsReader assetsReader, IAssetsPatchWriter assetsPatchWriter)
        : this(assetsReader, assetsPatchWriter, new ModManifestLoader()) { }

    public AssetsWorkflowService(IAssetsFileService assetsFileService, GameDirectoryResolver gameDirectoryResolver)
        : this(assetsFileService, assetsFileService, new ModManifestLoader(), gameDirectoryResolver) { }

    public AssetsWorkflowService(
        IAssetsReader assetsReader,
        IAssetsPatchWriter assetsPatchWriter,
        IModManifestLoader manifestLoader)
        : this(assetsReader, assetsPatchWriter, manifestLoader, new GameDirectoryResolver()) { }

    public AssetsWorkflowService(
        IAssetsReader assetsReader,
        IAssetsPatchWriter assetsPatchWriter,
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver)
    {
        var assetQueryService = new AssetQueryService(assetsReader);
        var valueResolver = new PatchValueResolver(assetQueryService);
        var fieldPatchPlanBuilder = new FieldPatchPlanBuilder(assetQueryService, valueResolver);
        var replacementPlanBuilder = new ReplacementPlanBuilder(assetQueryService);
        var patchPlanBuilder = new PatchPlanBuilder(fieldPatchPlanBuilder, replacementPlanBuilder);
        var patchOutputWriter = new PatchOutputWriter(assetsPatchWriter);
        Action releaseReadResources = assetsReader is IDisposable disposable ? disposable.Dispose : static () => { };
        var patchAssetsWorkflow = new PatchAssetsWorkflow(
            patchPlanBuilder,
            patchOutputWriter,
            releaseReadResources);

        _installModWorkflow = new InstallModWorkflow(
            patchAssetsWorkflow,
            new InstallPayloadPlanner(),
            manifestLoader,
            gameDirectoryResolver);
    }

    public InstallPreviewResult PreviewInstallMod(InstallPreviewRequest request)
    {
        return _installModWorkflow.Preview(request);
    }

    public InstallModResult InstallMod(InstallModRequest request)
    {
        return _installModWorkflow.Install(request);
    }
}
