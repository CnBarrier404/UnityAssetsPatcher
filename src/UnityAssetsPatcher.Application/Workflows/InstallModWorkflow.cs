using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly PatchAssetsWorkflow _patchAssetsWorkflow;
    private readonly IAssetsAccessScope _assets;
    private readonly IModManifestLoader _manifestLoader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;

    public InstallModWorkflow(
        PatchAssetsWorkflow patchAssetsWorkflow,
        IAssetsAccessScope assets,
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive)
    {
        _patchAssetsWorkflow = patchAssetsWorkflow;
        _assets = assets;
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _openPackageArchive = openPackageArchive;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new WorkflowTiming();
        using PackageSource source =
            new PackageSourceLoader(_manifestLoader, _gameDirectoryResolver, _openPackageArchive)
                .Execute(request.ZipFilePath, request.GameDirectory, timings);

        try
        {
            TargetAssetSet targets = new TargetAssetResolver()
                .Execute(source.GameDirectory, source.Manifest, timings);
            PayloadPlan payloadPlan = new PayloadPlanner().Plan(
                source,
                targets,
                requireAvailableDestination: false);
            PatchAssetPreview patchPreview = _patchAssetsWorkflow.Preview(source, targets, timings);
            PayloadPreview payloadPreview = PayloadPlanner.Preview(payloadPlan);

            return new InstallPreviewResult(
                source.Manifest.Name,
                source.Manifest.Version,
                source.Manifest.Author,
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
        using PackageSource source =
            new PackageSourceLoader(_manifestLoader, _gameDirectoryResolver, _openPackageArchive)
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
            PatchAssetPlan patchPlan = _patchAssetsWorkflow.Plan(source, targets, timings);
            var recordStore = new ModInstallationStore(request.BackupDirectory);
            string installDirectory = recordStore.CreateInstallDirectory(source.Manifest.Name, source.Manifest.Version);
            string assetsBackupDirectory = Path.Combine(installDirectory, "assets");
            ReleaseReadResources();
            PatchAssetApplyResult patchApplyResult = _patchAssetsWorkflow.Apply(
                patchPlan,
                assetsBackupDirectory,
                timings);
            PayloadCopyResult copiedFiles = new PayloadCopier().Execute(payloadPlan, timings);
            recordStore.Save(
                CreateInstallRecord(source, patchApplyResult, copiedFiles),
                installDirectory);

            return new InstallModResult(
                source.Manifest.Name,
                source.Manifest.Version,
                source.Manifest.Author,
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
        _assets.ReleaseReadResources();
    }

    private static InstallRecord CreateInstallRecord(
        PackageSource source,
        PatchAssetApplyResult patchApplyResult,
        PayloadCopyResult copiedFiles)
    {
        return new InstallRecord(
            Guid.NewGuid().ToString("N"),
            InstallRecordStatus.Installed,
            DateTimeOffset.Now,
            null,
            source.Manifest.Name,
            source.Manifest.Version,
            source.Manifest.Author,
            source.PackagePath,
            source.GameDirectory,
            patchApplyResult.Files
                .Select(file => new InstallRecordPatchedFile(
                    file.Target,
                    file.AssetsFilePath,
                    file.BackupPath,
                    null,
                    file.AssetCount,
                    file.OperationCount))
                .ToArray(),
            copiedFiles.Files
                .Select(file => new InstallRecordCopiedFile(
                    file.Source,
                    file.DestinationPath,
                    File.Exists(file.DestinationPath)))
                .ToArray());
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
