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
    private readonly ModManifestReader _manifestReader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;

    public InstallModWorkflow(
        PatchAssetsWorkflow patchAssetsWorkflow,
        IAssetsAccessScope assets,
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive)
    {
        _patchAssetsWorkflow = patchAssetsWorkflow;
        _assets = assets;
        _manifestReader = manifestReader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _openPackageArchive = openPackageArchive;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new WorkflowTiming();
        using ModPackage package = ModPackage.Load(
            request.ZipFilePath,
            request.GameDirectory,
            _manifestReader,
            _gameDirectoryResolver,
            _openPackageArchive,
            timings);

        try
        {
            TargetAssetSet targets = new TargetAssetResolver()
                .Execute(package.GameDirectory, package.Manifest, timings);
            PayloadPlan payloadPlan = package.PlanPayload(
                targets,
                requireAvailableDestination: false);
            PatchAssetPreview patchPreview = _patchAssetsWorkflow.Preview(package, targets, timings);
            PayloadPreview payloadPreview = ModPackage.PreviewPayload(payloadPlan);

            return new InstallPreviewResult(
                package.Manifest.Name,
                package.Manifest.Version,
                package.Manifest.Author,
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
        using ModPackage package = ModPackage.Load(
            request.ZipFilePath,
            request.GameDirectory,
            _manifestReader,
            _gameDirectoryResolver,
            _openPackageArchive,
            timings);

        try
        {
            new ManifestPatchOperationValidator().Execute(package.Manifest);

            TargetAssetSet targets = new TargetAssetResolver()
                .Execute(package.GameDirectory, package.Manifest, timings);
            PayloadPlan payloadPlan = package.PlanPayload(
                targets,
                requireAvailableDestination: true);
            PatchAssetPlan patchPlan = _patchAssetsWorkflow.Plan(package, targets, timings);
            var recordStore = new ModInstallationStore(request.BackupDirectory);
            string installDirectory =
                recordStore.CreateInstallDirectory(package.Manifest.Name, package.Manifest.Version);
            string assetsBackupDirectory = Path.Combine(installDirectory, "assets");
            ReleaseReadResources();
            PatchAssetApplyResult patchApplyResult = _patchAssetsWorkflow.Apply(
                patchPlan,
                assetsBackupDirectory,
                timings);
            PayloadCopyResult copiedFiles = ModPackage.CopyPayload(payloadPlan, timings);
            recordStore.Save(
                CreateInstallRecord(package, patchApplyResult, copiedFiles),
                installDirectory);

            return new InstallModResult(
                package.Manifest.Name,
                package.Manifest.Version,
                package.Manifest.Author,
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
        ModPackage package,
        PatchAssetApplyResult patchApplyResult,
        PayloadCopyResult copiedFiles)
    {
        return new InstallRecord(
            Guid.NewGuid().ToString("N"),
            InstallRecordStatus.Installed,
            DateTimeOffset.Now,
            null,
            package.Manifest.Name,
            package.Manifest.Version,
            package.Manifest.Author,
            package.PackagePath,
            package.GameDirectory,
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
