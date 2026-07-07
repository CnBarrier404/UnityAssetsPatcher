using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Installation;

public sealed class InstallPlanner
{
    private readonly ModManifestReader _manifestReader;
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly PatchPlanBuilder _patchPlanBuilder;

    public InstallPlanner(
        ModManifestReader manifestReader,
        TargetAssetResolver targetAssetResolver,
        GameDirectoryResolver gameDirectoryResolver,
        PatchPlanBuilder patchPlanBuilder)
    {
        _manifestReader = manifestReader;
        _targetAssetResolver = targetAssetResolver;
        _gameDirectoryResolver = gameDirectoryResolver;
        _patchPlanBuilder = patchPlanBuilder;
    }

    public InstallPlanSession BuildPreview(InstallPreviewRequest request, StepTimer timings)
    {
        ModPackage? package = null;

        try
        {
            package = OpenPackage(request.ZipFilePath, request.SelectedOptionalGroups, timings);
            string gameDirectory =
                _gameDirectoryResolver.ResolveRequired(request.GameDirectory, package.Manifest.Info.Game);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadFiles = PlanPayloadFiles(package.Manifest, targets);
            InstallPatchPreview patchPreview = CreatePatchPreview(targets, package, timings);
            var payloadPreview = PreviewPayloadFiles(payloadFiles);
            var plan = new InstallPlan(gameDirectory, new InstallPreviewPlan(patchPreview, payloadPreview), null);
            var session = new InstallPlanSession(package, plan);
            package = null;
            return session;
        }
        finally
        {
            package?.Dispose();
        }
    }

    public InstallPlanSession BuildInstall(InstallModRequest request, StepTimer timings)
    {
        ModPackage? package = null;

        try
        {
            package = OpenPackage(request.ZipFilePath, request.SelectedOptionalGroups, timings);
            string gameDirectory =
                _gameDirectoryResolver.ResolveRequired(request.GameDirectory, package.Manifest.Info.Game);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadFiles = PlanPayloadFiles(package.Manifest, targets);
            InstallPatchPlan patchWritePlan = CreateRequiredPatchWritePlan(targets, package, timings);
            var plan = new InstallPlan(gameDirectory, null, new InstallWritePlan(patchWritePlan, payloadFiles));
            var session = new InstallPlanSession(package, plan);
            package = null;
            return session;
        }
        finally
        {
            package?.Dispose();
        }
    }

    private ModPackage OpenPackage(string zipFilePath, IReadOnlyList<string> selectedOptionalGroups, StepTimer timings)
    {
        return ModPackage.Open(zipFilePath, selectedOptionalGroups, _manifestReader, timings);
    }

    private InstallPatchPreview CreatePatchPreview(TargetAssetSet targets, ModPackage package, StepTimer timings)
    {
        var files = timings.Measure("analyze-changes", () => targets.Targets
            .Select(target =>
            {
                var preview = _patchPlanBuilder.CreatePreview(
                    target.AssetsFilePath,
                    target.Patches,
                    package.PatchSourcePaths);

                return new InstallPatchPreviewFile(target.Name, target.AssetsFilePath, preview);
            })
            .ToArray());

        return new InstallPatchPreview(files);
    }

    private InstallPatchPlan CreateRequiredPatchWritePlan(TargetAssetSet targets, ModPackage package, StepTimer timings)
    {
        PatchOperationRules.ValidateModManifest(package.Manifest);

        var files = timings.Measure("analyze-changes", () => targets.Targets
            .Select(target =>
            {
                PatchFileWritePlan patchPlan = _patchPlanBuilder.CreateRequiredWritePlan(
                    target.AssetsFilePath,
                    target.Patches,
                    package.PatchSourcePaths);

                return new InstallPatchPlanFile(target.Name, target.AssetsFilePath, patchPlan);
            })
            .ToArray());

        return new InstallPatchPlan(files);
    }

    public static IReadOnlyList<InstallPayloadFilePlan> PlanPayloadFiles(ModManifest manifest, TargetAssetSet targets)
    {
        if (manifest.Files.Count == 0)
        {
            return [];
        }

        string payloadDirectory = ResolvePayloadDirectory(targets.AssetsFilePaths);
        var files = new List<InstallPayloadFilePlan>();

        foreach (ManifestFile file in manifest.Files)
        {
            string entryPath = file.Source.Replace('\\', '/');
            string fileName = Path.GetFileName(entryPath.Replace('/', Path.DirectorySeparatorChar));

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException($"Payload source must name a file: {entryPath}");
            }

            files.Add(new InstallPayloadFilePlan(entryPath, Path.Combine(payloadDirectory, fileName)));
        }

        return files;
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

    private static IReadOnlyList<InstallChange> PreviewPayloadFiles(IReadOnlyList<InstallPayloadFilePlan> files)
    {
        return files
            .Select(file => new InstallChange(
                InstallChangeKind.Payload,
                file.Source,
                file.DestinationPath,
                WillCopy: !File.Exists(file.DestinationPath)))
            .ToArray();
    }
}

public sealed class InstallPlanSession : IDisposable
{
    public ModPackage Package { get; }
    public InstallPlan Plan { get; }

    public InstallPlanSession(ModPackage package, InstallPlan plan)
    {
        Package = package;
        Plan = plan;
    }

    public void Dispose()
    {
        Package.Dispose();
    }
}

public sealed record InstallPlan(string GameDirectory, InstallPreviewPlan? Preview, InstallWritePlan? Write);

public sealed record InstallPreviewPlan(InstallPatchPreview Patch, IReadOnlyList<InstallChange> Payload);

public sealed record InstallWritePlan(InstallPatchPlan Patch, IReadOnlyList<InstallPayloadFilePlan> PayloadFiles);

public sealed record InstallPayloadFilePlan(string Source, string DestinationPath);

public sealed record InstallPatchPreview(IReadOnlyList<InstallPatchPreviewFile> Files);

public sealed record InstallPatchPreviewFile(string Target, string AssetsFilePath, PatchPreviewResult Preview);

public sealed record InstallPatchPlan(IReadOnlyList<InstallPatchPlanFile> Files);

public sealed record InstallPatchPlanFile(string Target, string AssetsFilePath, PatchFileWritePlan PatchPlan);
