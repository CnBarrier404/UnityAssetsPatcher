using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class PatchAssetsWorkflow
{
    private readonly PatchPlanBuilder _patchPlanBuilder;
    private readonly PatchOutputWriter _patchOutputWriter;

    public PatchAssetsWorkflow(PatchPlanBuilder patchPlanBuilder, PatchOutputWriter patchOutputWriter)
    {
        _patchPlanBuilder = patchPlanBuilder;
        _patchOutputWriter = patchOutputWriter;
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

    public PatchApplyResult ApplyTargets(
        string assetsFilePath,
        string? outputPath,
        string backupDirectory,
        IReadOnlyList<ManifestPatch> targets,
        string configPath)
    {
        PatchFileWritePlan plan = CreateWritePlan(assetsFilePath, targets, configPath);

        return _patchOutputWriter.Write(assetsFilePath, outputPath, backupDirectory, plan);
    }

    public PatchApplyResult WritePlanInPlace(
        string assetsFilePath,
        string backupDirectory,
        PatchFileWritePlan plan)
    {
        return _patchOutputWriter.Write(assetsFilePath, null, backupDirectory, plan);
    }
}
