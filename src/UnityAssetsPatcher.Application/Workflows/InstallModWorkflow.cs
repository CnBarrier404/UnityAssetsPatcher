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
            request.SelectedOptionalGroups,
            _manifestReader,
            _openPackageArchive,
            timings);

        try
        {
            string gameDirectory = ResolveGameDirectory(request.GameDirectory, package.Manifest);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadFiles = PlanPayload(
                package,
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

            var payloadPreview = PreviewPayload(payloadFiles);

            return new InstallPreviewResult(
                package.Manifest.Name,
                package.Manifest.Version,
                package.Manifest.Author,
                ToInstallPreviewFiles(patchPreview),
                payloadPreview,
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
            request.SelectedOptionalGroups,
            _manifestReader,
            _openPackageArchive,
            timings);

        try
        {
            PatchOperationRules.ValidateModManifest(package.Manifest);

            string gameDirectory = ResolveGameDirectory(request.GameDirectory, package.Manifest);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadFiles = PlanPayload(
                package,
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

            var copiedFiles = CopyPayload(package, payloadFiles, timings);
            var appliedOptionalGroups =
                ResolveAppliedOptionalGroups(package, request.SelectedOptionalGroups);
            recordStore.Save(
                CreateInstallRecord(package, gameDirectory, patchApplyResult, copiedFiles, appliedOptionalGroups),
                installDirectory);

            return new InstallModResult(
                package.Manifest.Name,
                package.Manifest.Version,
                package.Manifest.Author,
                ToInstallModFiles(patchApplyResult),
                copiedFiles,
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
        string gameDirectory,
        PatchAssetApplyResult patchApplyResult,
        IReadOnlyList<InstallCopiedFileResult> copiedFiles,
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
            gameDirectory,
            patchApplyResult.Files
                .Select(file => new InstallRecordPatchedFile(
                    file.Target,
                    file.AssetsFilePath,
                    file.BackupPath,
                    null,
                    file.AssetCount,
                    file.OperationCount))
                .ToArray(),
            copiedFiles
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

    private string ResolveGameDirectory(string? gameDirectory, ModManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(gameDirectory))
        {
            string fullGameDirectory = Path.GetFullPath(gameDirectory);

            return Directory.Exists(fullGameDirectory)
                ? fullGameDirectory
                : throw new DirectoryNotFoundException($"Game directory not found: {fullGameDirectory}");
        }

        if (string.IsNullOrWhiteSpace(manifest.Game))
        {
            throw new DirectoryNotFoundException(
                "Game directory was not provided and manifest does not contain a 'game' property.");
        }

        string? resolvedDirectory = _gameDirectoryResolver.Resolve(manifest.Game);

        return resolvedDirectory ?? throw new DirectoryNotFoundException(
            $"Game directory could not be resolved for manifest game: {manifest.Game}");
    }

    private static (string Source, string DestinationPath)[] PlanPayload(
        ModPackage package,
        TargetAssetSet targets,
        bool requireAvailableDestination)
    {
        if (package.Manifest.Files.Count == 0)
        {
            return [];
        }

        string payloadDirectory = ResolvePayloadDirectory(targets.AssetsFilePaths);
        var files = new List<(string Source, string DestinationPath)>();

        foreach (ManifestFile file in package.Manifest.Files)
        {
            string entryPath = file.Source.Replace('\\', '/');

            string fileName = Path.GetFileName(entryPath.Replace('/', Path.DirectorySeparatorChar));

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException($"Payload source must name a file: {entryPath}");
            }

            string destinationPath = Path.Combine(payloadDirectory, fileName);

            if (requireAvailableDestination && File.Exists(destinationPath))
            {
                throw new IOException($"Payload file already exists: {destinationPath}");
            }

            files.Add((entryPath, destinationPath));
        }

        return files.ToArray();
    }

    private static InstallCopyFilePreviewResult[] PreviewPayload(
        IReadOnlyList<(string Source, string DestinationPath)> files)
    {
        return files
            .Select(file => new InstallCopyFilePreviewResult(
                file.Source,
                file.DestinationPath,
                !File.Exists(file.DestinationPath)))
            .ToArray();
    }

    private static InstallCopiedFileResult[] CopyPayload(
        ModPackage package,
        IReadOnlyList<(string Source, string DestinationPath)> files,
        StepTimer timings)
    {
        return timings.Measure("copy-files", () =>
        {
            if (files.Count == 0)
            {
                return [];
            }

            var results = new List<InstallCopiedFileResult>();

            foreach ((string source, string destinationPath) in files)
            {
                package.CopyEntryToFile(source, destinationPath);
                results.Add(new InstallCopiedFileResult(source, destinationPath));
            }

            return results.ToArray();
        });
    }

    private static string ResolvePayloadDirectory(IEnumerable<string> targetAssetsFilePaths)
    {
        string[] targetDirectories = targetAssetsFilePaths
            .Select(path => Path.GetDirectoryName(Path.GetFullPath(path)) ??
                            throw new InvalidOperationException(
                                $"Cannot resolve directory for assets file: {path}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return targetDirectories.Length switch
        {
            1 => targetDirectories[0],
            0 => throw new InvalidOperationException("Payload files require at least one patch target."),
            _ => throw new InvalidOperationException(
                "Payload files require all patch targets to resolve to the same directory.")
        };
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
}
