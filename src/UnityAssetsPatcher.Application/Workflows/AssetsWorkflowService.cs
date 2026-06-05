using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class AssetsWorkflowService
{
    private readonly InspectAssetsWorkflow _inspectAssetsWorkflow;
    private readonly FindAssetsWorkflow _findAssetsWorkflow;
    private readonly PatchAssetsWorkflow _patchAssetsWorkflow;
    private readonly InstallModWorkflow _installModWorkflow;

    public AssetsWorkflowService(IAssetsFileService assetsFileService)
        : this(assetsFileService, assetsFileService) { }

    public AssetsWorkflowService(IAssetsReader assetsReader, IAssetsPatchWriter assetsPatchWriter)
    {
        var assetQueryService = new AssetQueryService(assetsReader);
        var valueResolver = new PatchValueResolver(assetQueryService);
        var fieldPatchPlanBuilder = new FieldPatchPlanBuilder(assetQueryService, valueResolver);
        var replacementPlanBuilder = new ReplacementPlanBuilder(assetQueryService);
        var patchPlanBuilder = new PatchPlanBuilder(fieldPatchPlanBuilder, replacementPlanBuilder);
        var patchOutputWriter = new PatchOutputWriter(assetsPatchWriter);
        Action releaseReadResources = assetsReader is IDisposable disposable ? disposable.Dispose : static () => { };
        var patchAssetsWorkflow = new PatchAssetsWorkflow(patchPlanBuilder, patchOutputWriter, releaseReadResources);

        _inspectAssetsWorkflow = new InspectAssetsWorkflow(assetsReader);
        _findAssetsWorkflow = new FindAssetsWorkflow(assetQueryService);
        _patchAssetsWorkflow = patchAssetsWorkflow;
        _installModWorkflow = new InstallModWorkflow(patchAssetsWorkflow, new InstallPayloadPlanner());
    }

    public IReadOnlyList<AssetsInfo> InspectList(InspectListRequest request)
    {
        return _inspectAssetsWorkflow.List(request);
    }

    public AssetsFieldInfo InspectFields(InspectFieldsRequest request)
    {
        return _inspectAssetsWorkflow.Fields(request);
    }

    public IReadOnlyList<AssetMatch> FindAssets(FindAssetsRequest request)
    {
        return _findAssetsWorkflow.Find(request);
    }

    public PatchPreviewResult PreviewPatch(PatchPreviewRequest request)
    {
        return _patchAssetsWorkflow.Preview(request);
    }

    public PatchApplyResult ApplyPatch(PatchApplyRequest request)
    {
        return _patchAssetsWorkflow.Apply(request);
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
