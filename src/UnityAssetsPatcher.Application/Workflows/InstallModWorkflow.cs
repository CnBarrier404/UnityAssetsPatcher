using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly PatchPlanBuilder _patchPlanBuilder;
    private readonly PatchOutputWriter _patchOutputWriter;
    private readonly IAssetsAccessScope _assets;
    private readonly ModManifestReader _manifestReader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly ModInstallationStoreFactory _recordStoreFactory;

    public InstallModWorkflow(
        PatchPlanBuilder patchPlanBuilder,
        PatchOutputWriter patchOutputWriter,
        IAssetsAccessScope assets,
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive,
        TargetAssetResolver targetAssetResolver,
        ModInstallationStoreFactory recordStoreFactory)
    {
        _patchPlanBuilder = patchPlanBuilder;
        _patchOutputWriter = patchOutputWriter;
        _assets = assets;
        _manifestReader = manifestReader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _openPackageArchive = openPackageArchive;
        _targetAssetResolver = targetAssetResolver;
        _recordStoreFactory = recordStoreFactory;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new StepTimer();
        using ModPackage package = ModPackage.Load(
            request.ZipFilePath,
            request.GameDirectory,
            request.SelectedOptionalGroups,
            _manifestReader,
            _gameDirectoryResolver,
            _openPackageArchive,
            timings);

        try
        {
            TargetAssetSet targets = _targetAssetResolver.Execute(package.GameDirectory, package.Manifest, timings);
            PayloadPlan payloadPlan = package.PlanPayload(
                targets,
                requireAvailableDestination: false);

            var patchFiles = timings.Measure("analyze-changes", () => targets.Targets
                .Select(target =>
                {
                    PatchPreviewResult preview = _patchPlanBuilder.CreatePreview(
                        target.AssetsFilePath,
                        target.Patches,
                        package.SourceAssetsPaths);

                    return new PatchAssetPreviewFile(target.Name, target.AssetsFilePath, preview);
                })
                .ToArray());
            var patchPreview = new PatchAssetPreview(patchFiles);

            PayloadPreview payloadPreview = ModPackage.PreviewPayload(payloadPlan);

            return new InstallPreviewResult(
                package.Manifest.Name,
                package.Manifest.Version,
                package.Manifest.Author,
                ToInstallPreviewFiles(patchPreview),
                ToInstallCopyPreviewFiles(payloadPreview),
                ToOptionalGroupPreviews(package.AvailableOptional),
                timings.BuildSnapshot());
        }
        finally
        {
            ReleaseReadResources();
        }
    }

    public InstallModResult Install(InstallModRequest request)
    {
        var timings = new StepTimer();
        using ModPackage package = ModPackage.Load(
            request.ZipFilePath,
            request.GameDirectory,
            request.SelectedOptionalGroups,
            _manifestReader,
            _gameDirectoryResolver,
            _openPackageArchive,
            timings);

        try
        {
            PatchOperationRules.ValidateModManifest(package.Manifest);

            TargetAssetSet targets = _targetAssetResolver.Execute(package.GameDirectory, package.Manifest, timings);
            PayloadPlan payloadPlan = package.PlanPayload(
                targets,
                requireAvailableDestination: true);

            var patchPlanFiles = timings.Measure("analyze-changes", () => targets.Targets
                .Select(target =>
                {
                    PatchFileWritePlan patchPlan = _patchPlanBuilder.CreateRequiredWritePlan(
                        target.AssetsFilePath,
                        target.Patches,
                        package.SourceAssetsPaths);

                    return new PatchAssetFilePlan(target.Name, target.AssetsFilePath, patchPlan);
                })
                .ToArray());
            var patchPlan = new PatchAssetPlan(patchPlanFiles);

            ModInstallationStore recordStore = _recordStoreFactory.Create(request.BackupDirectory);
            string installDirectory =
                recordStore.CreateInstallDirectory(package.Manifest.Name, package.Manifest.Version);
            string assetsBackupDirectory = Path.Combine(installDirectory, "assets");
            ReleaseReadResources();

            var patchApplyFiles = timings.Measure("apply-patches", () => patchPlan.Files
                .Select(file =>
                {
                    PatchApplyResult result = _patchOutputWriter.Write(
                        file.AssetsFilePath,
                        null,
                        assetsBackupDirectory,
                        file.PatchPlan);

                    return new { File = file, Result = result };
                })
                .Where(item => item.Result.OperationCount != 0)
                .Select(item =>
                {
                    string backupPath = item.Result.BackupPath ??
                                        throw new InvalidOperationException("Patch write did not create a backup.");

                    return new PatchAssetAppliedFile(
                        item.File.Target,
                        item.Result.OutputPath,
                        backupPath,
                        item.Result.AssetCount,
                        item.Result.OperationCount);
                })
                .ToArray());
            var patchApplyResult = new PatchAssetApplyResult(patchApplyFiles);

            PayloadCopyResult copiedFiles = ModPackage.CopyPayload(payloadPlan, timings);
            IReadOnlyList<string> appliedOptionalGroups =
                ResolveAppliedOptionalGroups(package, request.SelectedOptionalGroups);
            recordStore.Save(
                CreateInstallRecord(package, patchApplyResult, copiedFiles, appliedOptionalGroups),
                installDirectory);

            return new InstallModResult(
                package.Manifest.Name,
                package.Manifest.Version,
                package.Manifest.Author,
                ToInstallModFiles(patchApplyResult),
                ToInstallCopiedFiles(copiedFiles),
                appliedOptionalGroups,
                timings.BuildSnapshot());
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
        PayloadCopyResult copiedFiles,
        IReadOnlyList<string> appliedOptionalGroups)
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
                .ToArray())
        {
            OptionalGroups = appliedOptionalGroups.Count == 0 ? null : appliedOptionalGroups,
        };
    }

    private static IReadOnlyList<string> ResolveAppliedOptionalGroups(
        ModPackage package,
        IReadOnlyList<string> selectedOptionalGroups)
    {
        if (selectedOptionalGroups.Count == 0)
        {
            return [];
        }

        var selected = new HashSet<string>(selectedOptionalGroups, StringComparer.OrdinalIgnoreCase);

        return package.AvailableOptional
            .Where(group => selected.Contains(group.Name))
            .Select(group => group.Name)
            .ToArray();
    }

    private static InstallPreviewFileResult[] ToInstallPreviewFiles(PatchAssetPreview preview)
    {
        return preview.Files
            .Select(file => new InstallPreviewFileResult(file.Target, file.AssetsFilePath, file.Preview))
            .ToArray();
    }

    private static OptionalGroupPreview[] ToOptionalGroupPreviews(IReadOnlyList<ManifestOptionalGroup> groups)
    {
        return groups
            .Select(group => new OptionalGroupPreview(group.Name, group.Description))
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
}
