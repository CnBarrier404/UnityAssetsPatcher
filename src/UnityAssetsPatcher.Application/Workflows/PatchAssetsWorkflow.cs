using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class PatchAssetsWorkflow
{
    private readonly PatchPlanBuilder _patchPlanBuilder;
    private readonly PatchOutputWriter _patchOutputWriter;
    private readonly IModManifestLoader _manifestLoader;
    private readonly ManifestTargetSelector _targetSelector;
    private readonly Action _releaseReadResources;
    private bool _readResourcesReleased;

    public PatchAssetsWorkflow(
        PatchPlanBuilder patchPlanBuilder,
        PatchOutputWriter patchOutputWriter,
        IModManifestLoader manifestLoader,
        ManifestTargetSelector targetSelector,
        Action? releaseReadResources = null)
    {
        _patchPlanBuilder = patchPlanBuilder;
        _patchOutputWriter = patchOutputWriter;
        _manifestLoader = manifestLoader;
        _targetSelector = targetSelector;
        _releaseReadResources = releaseReadResources ?? (() => { });
    }

    public PatchPreviewResult Preview(PatchPreviewRequest request)
    {
        ModManifest manifest = _manifestLoader.Load(request.ConfigPath);
        IReadOnlyList<ManifestPatch> targets = _targetSelector.ForAssetsFile(manifest, request.AssetsFilePath);

        return _patchPlanBuilder.CreatePreview(request.AssetsFilePath, targets, request.ConfigPath);
    }

    public PatchApplyResult Apply(PatchApplyRequest request)
    {
        ModManifest manifest = _manifestLoader.Load(request.ConfigPath);
        IReadOnlyList<ManifestPatch> targets = _targetSelector.ForAssetsFile(manifest, request.AssetsFilePath);
        PatchFileWritePlan plan = _patchPlanBuilder.CreateRequiredWritePlan(
            request.AssetsFilePath,
            targets,
            request.ConfigPath);

        ReleaseReadResources();

        return _patchOutputWriter.Write(
            request.AssetsFilePath,
            request.OutputPath,
            request.BackupDirectory,
            plan);
    }

    public PatchAssetPreview Preview(PackageSource source, TargetAssetSet targets, WorkflowTiming timings)
    {
        return new PatchPlanner(_patchPlanBuilder).Preview(source, targets, timings);
    }

    public PatchAssetPlan Plan(PackageSource source, TargetAssetSet targets, WorkflowTiming timings)
    {
        return new PatchPlanner(_patchPlanBuilder).Plan(source, targets, timings);
    }

    public PatchAssetApplyResult Apply(PatchAssetPlan plan, string backupDirectory, WorkflowTiming timings)
    {
        return new PatchAssetApplier(_patchOutputWriter).Execute(plan, backupDirectory, timings);
    }

    private void ReleaseReadResources()
    {
        if (_readResourcesReleased)
        {
            return;
        }

        _readResourcesReleased = true;
        _releaseReadResources();
    }
}
