using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly PatchPlanBuilder _patchPlanBuilder;
    private readonly PatchOutputWriter _patchOutputWriter;
    private readonly Action _releaseReadResources;
    private readonly IModManifestLoader _manifestLoader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;

    public InstallModWorkflow(
        PatchPlanBuilder patchPlanBuilder,
        PatchOutputWriter patchOutputWriter,
        Action releaseReadResources,
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver)
    {
        _patchPlanBuilder = patchPlanBuilder;
        _patchOutputWriter = patchOutputWriter;
        _releaseReadResources = releaseReadResources;
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new WorkflowTiming();
        using PackageSource source = new PackageSourceLoader(_manifestLoader, _gameDirectoryResolver)
            .Execute(request.ZipFilePath, request.GameDirectory, timings);

        try
        {
            TargetAssetSet targets = new TargetAssetResolver()
                .Execute(source.GameDirectory, source.Manifest, timings);
            PayloadPlan payloadPlan = new PayloadPlanner().Plan(
                source,
                targets,
                requireAvailableDestination: false);
            PatchAssetPreview patchPreview = new PatchPlanner(_patchPlanBuilder)
                .Preview(source, targets, timings);
            PayloadPreview payloadPreview = PayloadPlanner.Preview(payloadPlan);

            return new InstallPreviewResult(
                source.Manifest.Name,
                source.Manifest.Version,
                ToInstallPreviewFiles(patchPreview),
                ToInstallCopyPreviewFiles(payloadPreview),
                ToInstallTiming(timings.Build()));
        }
        finally
        {
            ReleaseReadResources();
        }
    }

    public InstallModResult Install(InstallModRequest request)
    {
        var timings = new WorkflowTiming();
        using PackageSource source = new PackageSourceLoader(_manifestLoader, _gameDirectoryResolver)
            .Execute(request.ZipFilePath, request.GameDirectory, timings);

        try
        {
            new ManifestPatchOperationValidator().Execute(source.Manifest);

            TargetAssetSet targets = new TargetAssetResolver()
                .Execute(source.GameDirectory, source.Manifest, timings);
            PayloadPlan payloadPlan = new PayloadPlanner().Plan(
                source,
                targets,
                requireAvailableDestination: true);
            PatchAssetPlan patchPlan = new PatchPlanner(_patchPlanBuilder)
                .Plan(source, targets, timings);
            ReleaseReadResources();
            PatchAssetApplyResult patchApplyResult = new PatchAssetApplier(_patchOutputWriter)
                .Execute(patchPlan, request.BackupDirectory, timings);
            PayloadCopyResult copiedFiles = new PayloadCopier().Execute(payloadPlan, timings);

            return new InstallModResult(
                source.Manifest.Name,
                source.Manifest.Version,
                ToInstallModFiles(patchApplyResult),
                ToInstallCopiedFiles(copiedFiles),
                ToInstallTiming(timings.Build()));
        }
        finally
        {
            ReleaseReadResources();
        }
    }

    private void ReleaseReadResources()
    {
        _releaseReadResources();
    }

    private static InstallPreviewFileResult[] ToInstallPreviewFiles(PatchAssetPreview preview)
    {
        return preview.Files
            .Select(file => new InstallPreviewFileResult(file.Target, file.AssetsFilePath, file.Preview))
            .ToArray();
    }

    private static InstallCopyFilePreviewResult[] ToInstallCopyPreviewFiles(PayloadPreview preview)
    {
        return preview.Files
            .Select(file => new InstallCopyFilePreviewResult(file.Source, file.DestinationPath, file.WillCopy))
            .ToArray();
    }

    private static InstallModFileResult[] ToInstallModFiles(PatchAssetApplyResult result)
    {
        return result.Files
            .Select(file => new InstallModFileResult(
                file.Target,
                file.AssetsFilePath,
                file.BackupPath,
                file.AssetCount,
                file.OperationCount))
            .ToArray();
    }

    private static InstallCopiedFileResult[] ToInstallCopiedFiles(PayloadCopyResult result)
    {
        return result.Files
            .Select(file => new InstallCopiedFileResult(file.Source, file.DestinationPath))
            .ToArray();
    }

    private static InstallTimingResult ToInstallTiming(WorkflowTimingSnapshot timing)
    {
        return new InstallTimingResult(
            timing.ReadPackage,
            timing.PrepareSources,
            timing.FindGameFiles,
            timing.AnalyzeChanges,
            timing.ApplyPatches,
            timing.CopyFiles,
            timing.Elapsed);
    }
}
