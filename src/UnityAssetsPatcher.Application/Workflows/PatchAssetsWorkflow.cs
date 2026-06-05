using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class PatchAssetsWorkflow
{
    private readonly PatchPlanBuilder _patchPlanBuilder;
    private readonly PatchOutputWriter _patchOutputWriter;
    private readonly Action _releaseReadResources;
    private bool _readResourcesReleased;

    public PatchAssetsWorkflow(
        PatchPlanBuilder patchPlanBuilder,
        PatchOutputWriter patchOutputWriter,
        Action releaseReadResources)
    {
        _patchPlanBuilder = patchPlanBuilder;
        _patchOutputWriter = patchOutputWriter;
        _releaseReadResources = releaseReadResources;
    }

    public PatchPreviewResult Preview(PatchPreviewRequest request)
    {
        ModManifest manifest = ModManifestLoader.Load(request.ConfigPath);
        var targets = PatchTargetSelector.ForAssetsFile(manifest, request.AssetsFilePath);

        return PreviewTargets(request.AssetsFilePath, targets, request.ConfigPath);
    }

    public PatchApplyResult Apply(PatchApplyRequest request)
    {
        ModManifest manifest = ModManifestLoader.Load(request.ConfigPath);
        var targets = PatchTargetSelector.ForAssetsFile(manifest, request.AssetsFilePath);

        return ApplyTargets(request.AssetsFilePath, request.OutputPath, request.BackupDirectory, targets,
            request.ConfigPath);
    }

    public PatchPreviewResult PreviewTargets(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        string configPath)
    {
        return _patchPlanBuilder.CreatePreview(assetsFilePath, targets, configPath);
    }

    public PatchFileWritePlan CreateWritePlan(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        string configPath)
    {
        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Patch config did not contain a target for assets file: {Path.GetFileName(assetsFilePath)}");
        }

        PatchFileWritePlan plan = _patchPlanBuilder.CreateWritePlan(assetsFilePath, targets, configPath);

        if (!plan.HasMatchedAssets)
        {
            throw new InvalidOperationException("Patch config did not match any assets.");
        }

        return plan;
    }

    private PatchApplyResult ApplyTargets(
        string assetsFilePath,
        string? outputPath,
        string backupDirectory,
        IReadOnlyList<ManifestPatch> targets,
        string configPath)
    {
        PatchFileWritePlan plan = CreateWritePlan(assetsFilePath, targets, configPath);

        ReleaseReadResources();

        return _patchOutputWriter.Write(assetsFilePath, outputPath, backupDirectory, plan);
    }

    public PatchApplyResult WritePlanInPlace(
        string assetsFilePath,
        string backupDirectory,
        PatchFileWritePlan plan)
    {
        ReleaseReadResources();

        return _patchOutputWriter.Write(assetsFilePath, null, backupDirectory, plan);
    }

    public void ReleaseReadResources()
    {
        if (_readResourcesReleased)
        {
            return;
        }

        _readResourcesReleased = true;
        _releaseReadResources();
    }
}
