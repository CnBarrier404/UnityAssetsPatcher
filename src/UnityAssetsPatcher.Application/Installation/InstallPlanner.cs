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

    internal InstallPlanSession<InstallPreviewPlan> BuildPreview(InstallRequest request, StepTimer timings)
    {
        ModPackage? package = null;

        try
        {
            package = OpenPackage(request.ZipFilePath, request.SelectedOptionalGroups, timings);
            string gameDirectory =
                _gameDirectoryResolver.ResolveRequired(request.GameDirectory, package.Manifest.Game);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadFiles = PlanPayloadFiles(package.Manifest, targets);
            IReadOnlyList<InstallPatchPreviewFile> patchPreview = CreatePatchPreview(targets, package, timings);
            var payloadPreview = PreviewPayloadFiles(payloadFiles);
            var plan = new InstallPreviewPlan(patchPreview, payloadPreview);
            var session = new InstallPlanSession<InstallPreviewPlan>(package, plan);
            package = null;
            return session;
        }
        finally
        {
            package?.Dispose();
        }
    }

    internal InstallPlanSession<InstallWritePlan> BuildInstall(InstallRequest request, StepTimer timings)
    {
        ModPackage? package = null;

        try
        {
            package = OpenPackage(request.ZipFilePath, request.SelectedOptionalGroups, timings);
            string gameDirectory =
                _gameDirectoryResolver.ResolveRequired(request.GameDirectory, package.Manifest.Game);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadFiles = PlanPayloadFiles(package.Manifest, targets);
            IReadOnlyList<InstallPatchPlanFile> patchWritePlan =
                CreateRequiredPatchWritePlan(targets, package, timings);
            var plan = new InstallWritePlan(gameDirectory, patchWritePlan, payloadFiles);
            var session = new InstallPlanSession<InstallWritePlan>(package, plan);
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

    private IReadOnlyList<InstallPatchPreviewFile> CreatePatchPreview(
        TargetAssetSet targets,
        ModPackage package,
        StepTimer timings)
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

        return files;
    }

    private IReadOnlyList<InstallPatchPlanFile> CreateRequiredPatchWritePlan(
        TargetAssetSet targets,
        ModPackage package,
        StepTimer timings)
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

        return files;
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
                file.DestinationPath))
            .ToArray();
    }
}

internal sealed class InstallPlanSession<TPlan> : IDisposable
    where TPlan : notnull
{
    public ModPackage Package { get; }
    public TPlan Plan { get; }

    public InstallPlanSession(ModPackage package, TPlan plan)
    {
        Package = package;
        Plan = plan;
    }

    public void Dispose()
    {
        Package.Dispose();
    }
}

internal sealed record InstallPreviewPlan(
    IReadOnlyList<InstallPatchPreviewFile> PatchFiles,
    IReadOnlyList<InstallChange> Payload);

internal sealed record InstallWritePlan(
    string GameDirectory,
    IReadOnlyList<InstallPatchPlanFile> PatchFiles,
    IReadOnlyList<InstallPayloadFilePlan> PayloadFiles);

public sealed record InstallPayloadFilePlan(string Source, string DestinationPath);

internal sealed record InstallPatchPreviewFile(string Target, string AssetsFilePath, PatchPreviewResult Preview);

internal sealed record InstallPatchPlanFile(string Target, string AssetsFilePath, PatchFileWritePlan PatchPlan);
