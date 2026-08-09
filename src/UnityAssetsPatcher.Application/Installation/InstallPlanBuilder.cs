using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;

namespace UnityAssetsPatcher.Application.Installation;

public enum InstallAnalysisMode
{
    PreviewDetailed,
    PreviewSummary,
    Apply
}

public sealed record InstallTargetAnalysis(string Target, string AssetsFilePath, PatchPlanningResult PlanningResult)
{
    public int MatchCount => PlanningResult.Plan switch
    {
        FieldPatchPlan fieldPlan => fieldPlan.Assets.Count,
        AssetReplacementPlan replacementPlan => replacementPlan.Replacements.Count,
        FieldPatchAndCopyPlan copyPlan => copyPlan.FieldPatches
            .Select(asset => asset.PathId)
            .Concat(copyPlan.Copies.Select(copy => copy.TargetPathId))
            .Distinct()
            .Count(),
        null => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(PlanningResult))
    };
}

public sealed record InstallAnalysis(
    ModManifest Manifest,
    string GameDirectory,
    IReadOnlyList<InstallTargetAnalysis> Targets,
    IReadOnlyList<InstallPayloadFilePlan> PayloadFiles,
    IReadOnlyList<ModOptionalGroup> OptionalGroups,
    IReadOnlyList<string> AppliedOptionalGroups);

public sealed record InstallPayloadFilePlan(string Source, string DestinationPath);

public sealed class InstallPlanBuilder
{
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly IReadOnlyList<IFieldPatchOperationHandler> _operationHandlers;

    public InstallPlanBuilder(
        TargetAssetResolver targetAssetResolver,
        GameDirectoryResolver gameDirectoryResolver,
        IEnumerable<IFieldPatchOperationHandler> operationHandlers)
    {
        _targetAssetResolver = targetAssetResolver;
        _gameDirectoryResolver = gameDirectoryResolver;
        _operationHandlers = operationHandlers.ToArray();
    }

    public InstallAnalysis Analyze(
        ModPackage package,
        string? requestedGameDirectory,
        InstallAnalysisMode mode,
        IAssetsFileReader assetsReader,
        StepTimer timings)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(assetsReader);
        ArgumentNullException.ThrowIfNull(timings);

        string gameDirectory = _gameDirectoryResolver.ResolveRequired(
            requestedGameDirectory,
            package.EffectiveManifest.Game);
        TargetAssetSet targetSet = _targetAssetResolver.Execute(gameDirectory, package.EffectiveManifest, timings);
        var payloadFiles = PlanPayloadFiles(package.EffectiveManifest, targetSet);

        if (mode == InstallAnalysisMode.Apply)
        {
            PatchOperationRules.ValidateModManifest(package.EffectiveManifest);
        }

        PatchPlanner patchPlanner = CreatePatchPlanner(assetsReader);
        var targets = timings.Measure(
            "analyze-changes",
            () => AnalyzeTargets(targetSet, package, mode, patchPlanner));

        return new InstallAnalysis(
            package.EffectiveManifest,
            gameDirectory,
            targets,
            payloadFiles,
            package.SourceManifest.OptionalGroups.ToArray(),
            package.AppliedOptionalGroups.ToArray());
    }

    public static IReadOnlyList<InstallPayloadFilePlan> PlanPayloadFiles(ModManifest manifest, TargetAssetSet targets)
    {
        if (manifest.Files.Count == 0)
        {
            return [];
        }

        string payloadDirectory = ResolvePayloadDirectory(targets.AssetsFilePaths);
        List<InstallPayloadFilePlan> payloadFiles = (from file in manifest.Files
            select file.Source.Replace('\\', '/')
            into entryPath
            let fileName = Path.GetFileName(entryPath.Replace('/', Path.DirectorySeparatorChar))
            select new InstallPayloadFilePlan(entryPath, Path.Combine(payloadDirectory, fileName))).ToList();
        HashSet<string> assetsPaths = targets.AssetsFilePaths.ToHashSet(TrustedPath.PathComparer);

        foreach (InstallPayloadFilePlan payloadFile in payloadFiles)
        {
            if (assetsPaths.Contains(payloadFile.DestinationPath))
            {
                throw new InvalidDataException(
                    $"Payload target conflicts with assets target: {payloadFile.DestinationPath}");
            }
        }

        return payloadFiles;
    }

    private static List<InstallTargetAnalysis> AnalyzeTargets(
        TargetAssetSet targets,
        ModPackage package,
        InstallAnalysisMode mode,
        PatchPlanner patchPlanner)
    {
        var results = new List<InstallTargetAnalysis>(targets.Targets.Count);

        foreach (TargetAsset target in targets.Targets)
        {
            PatchPlanningResult planningResult = patchPlanner.Plan(new PatchPlanningRequest(
                target.AssetsFilePath,
                target.Patches,
                package.PatchSourcePaths)
            {
                IncludePreviewDetails = mode == InstallAnalysisMode.PreviewDetailed
            });

            if (mode == InstallAnalysisMode.Apply && !planningResult.CanApply)
            {
                throw new PatchPlanningException(
                    planningResult.Diagnostic ?? throw new InvalidOperationException(
                        "Patch planning failed without a diagnostic."));
            }

            if (mode != InstallAnalysisMode.PreviewDetailed)
            {
                planningResult = planningResult with
                {
                    Preview = planningResult.Preview with { Assets = [] }
                };
            }

            results.Add(new InstallTargetAnalysis(
                target.Name,
                target.AssetsFilePath,
                planningResult));
        }

        return results;
    }

    private PatchPlanner CreatePatchPlanner(IAssetsFileReader assetsReader)
    {
        var assetQueryService = new AssetQueryService(assetsReader);
        var fieldPatchPlanner = new FieldPatchPlanner(assetQueryService, _operationHandlers);
        var replacementPlanner = new ReplacementPlanner(assetQueryService);
        var copyAssetPlanner = new CopyAssetPlanner(assetQueryService);

        return new PatchPlanner(fieldPatchPlanner, replacementPlanner, copyAssetPlanner);
    }

    private static string ResolvePayloadDirectory(IEnumerable<string> targetAssetsFilePaths)
    {
        string[] targetDirectories =
        [
            .. targetAssetsFilePaths
                .Select(path => Path.GetDirectoryName(Path.GetFullPath(path)) ??
                                throw new InvalidOperationException(
                                    $"Cannot resolve directory for assets file: {path}"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        return targetDirectories.Length switch
        {
            1 => targetDirectories[0],
            _ => throw new InvalidOperationException(
                "Payload files require all patch targets to resolve to the same directory.")
        };
    }
}
