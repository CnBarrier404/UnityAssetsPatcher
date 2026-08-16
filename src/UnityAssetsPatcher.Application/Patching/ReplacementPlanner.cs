using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class ReplacementPlanner
{
    private readonly AssetQueryService _assetQueryService;

    public ReplacementPlanner(AssetQueryService assetQueryService)
    {
        _assetQueryService = assetQueryService;
    }

    public IReadOnlyList<AssetReplacement> CreateWritePlan(
        string assetsFilePath,
        IReadOnlyList<ModPatch> targets,
        IReadOnlyDictionary<string, string> sourceAssetsPaths)
    {
        return Plan(assetsFilePath, targets, sourceAssetsPaths).Replacements;
    }

    public ReplacementPlanningOutput Plan(
        string assetsFilePath,
        IReadOnlyList<ModPatch> targets,
        IReadOnlyDictionary<string, string> sourceAssetsPaths)
    {
        var matches = FindReplacementMatches(
            assetsFilePath, targets, sourceAssetsPaths).ToArray();
        var replacements = matches
            .Select(match => new AssetReplacement(
                match.SourceAssetsFilePath,
                match.Source.PathId,
                match.Target.PathId))
            .ToArray();
        var assets = matches
            .Select(match =>
            {
                var operation = new PatchPreviewOperationResult(
                    "*",
                    $"Path ID {match.Target.PathId}",
                    match.MatchValue,
                    $"Path ID {match.Source.PathId} from {match.SourceAssetsFilePath}",
                    true);
                return new PatchPreviewAssetResult(match.Target, [operation]);
            })
            .ToArray();

        return new ReplacementPlanningOutput(replacements, new PatchPreviewResult(assets));
    }

    public PatchPreviewResult CreatePreview(
        string assetsFilePath,
        IReadOnlyList<ModPatch> targets,
        IReadOnlyDictionary<string, string> sourceAssetsPaths)
    {
        return Plan(assetsFilePath, targets, sourceAssetsPaths).Preview;
    }

    private IEnumerable<AssetReplacementMatch> FindReplacementMatches(
        string targetAssetsFilePath,
        IReadOnlyList<ModPatch> targets,
        IReadOnlyDictionary<string, string> sourceAssetsPaths)
    {
        AssetQueryContext? targetContext = null;
        var sourceIndexesByPath =
            new Dictionary<string, ReplacementSourceIndexes>(StringComparer.OrdinalIgnoreCase);

        foreach (ModPatch patch in targets)
        {
            if (patch.ReplaceAsset is not { } replaceAsset)
            {
                continue;
            }

            string sourceAssetsFilePath =
                ResolveReplacementAssetsFilePath(sourceAssetsPaths, replaceAsset.SourceAssetsFile);
            var seenTargetValues = new Dictionary<string, long>(StringComparer.Ordinal);
            targetContext ??= _assetQueryService.CreateContext(targetAssetsFilePath);

            foreach (AssetQueryMatch targetMatch in AssetQueryService.FindMatches(targetContext, patch))
            {
                string matchValue = ReadReplacementMatchValue(
                    targetMatch.FieldTree,
                    replaceAsset.MatchFieldPath,
                    targetMatch.Asset.PathId,
                    "target");

                if (!seenTargetValues.TryAdd(matchValue, targetMatch.Asset.PathId))
                {
                    throw new PatchPlanningException(
                        PatchDiagnosticCode.ReplacementMatchInvalid,
                        $"Replacement target contains multiple '{patch.AssetTypeName}' assets with {replaceAsset.MatchFieldPath} '{matchValue}'.");
                }

                if (!sourceIndexesByPath.TryGetValue(sourceAssetsFilePath, out ReplacementSourceIndexes? indexes))
                {
                    indexes = new ReplacementSourceIndexes(
                        _assetQueryService.CreateContext(sourceAssetsFilePath));
                    sourceIndexesByPath.Add(sourceAssetsFilePath, indexes);
                }

                var sourceMatches = indexes
                    .GetIndex(patch.AssetTypeName, replaceAsset.MatchFieldPath)
                    .GetValueOrDefault(matchValue, []);

                yield return sourceMatches.Count switch
                {
                    0 => throw new PatchPlanningException(
                        PatchDiagnosticCode.ReplacementMatchInvalid,
                        $"Replacement source did not contain a '{patch.AssetTypeName}' asset with {replaceAsset.MatchFieldPath} '{matchValue}'."),
                    > 1 => throw new PatchPlanningException(
                        PatchDiagnosticCode.ReplacementMatchInvalid,
                        $"Replacement source contains multiple '{patch.AssetTypeName}' assets with {replaceAsset.MatchFieldPath} '{matchValue}'."),
                    _ => new AssetReplacementMatch(
                        targetMatch.Asset,
                        sourceMatches[0],
                        matchValue,
                        sourceAssetsFilePath)
                };
            }
        }
    }

    private static string ReadReplacementMatchValue(
        AssetField fieldTree,
        string matchFieldPath,
        long pathId,
        string role)
    {
        AssetField? field = AssetFieldNavigator.Find(fieldTree, matchFieldPath);

        return field?.Value?.ToInvariantString() ?? throw new PatchPlanningException(
            PatchDiagnosticCode.ReplacementMatchInvalid,
            $"Replacement {role} Path ID {pathId} does not contain scalar match field '{matchFieldPath}'.");
    }

    private static string ResolveReplacementAssetsFilePath(
        IReadOnlyDictionary<string, string> sourceAssetsPaths,
        string assetsFilePath)
    {
        string normalizedPath = assetsFilePath.Replace('\\', '/');

        if (sourceAssetsPaths.TryGetValue(normalizedPath, out string? resolvedPath))
        {
            return resolvedPath;
        }

        throw new PatchPlanningException(
            PatchDiagnosticCode.ReplacementSourceNotFound,
            $"Replacement source assets file not found in package: {assetsFilePath}");
    }

    private sealed record AssetReplacementMatch(
        AssetInfo Target,
        AssetInfo Source,
        string MatchValue,
        string SourceAssetsFilePath);

    private sealed class ReplacementSourceIndexes
    {
        private readonly AssetQueryContext _context;

        private readonly Dictionary<string, Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<AssetInfo>>>>
            _indexesByType = new(StringComparer.OrdinalIgnoreCase);

        public ReplacementSourceIndexes(AssetQueryContext context)
        {
            _context = context;
        }

        public IReadOnlyDictionary<string, IReadOnlyList<AssetInfo>> GetIndex(
            string assetTypeName,
            string matchFieldPath)
        {
            if (!_indexesByType.TryGetValue(assetTypeName, out var indexesByFieldPath))
            {
                indexesByFieldPath = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<AssetInfo>>>(
                    StringComparer.Ordinal);
                _indexesByType.Add(assetTypeName, indexesByFieldPath);
            }

            if (indexesByFieldPath.TryGetValue(matchFieldPath, out var index))
            {
                return index;
            }

            index = BuildIndex(assetTypeName, matchFieldPath);
            indexesByFieldPath.Add(matchFieldPath, index);
            return index;
        }

        private IReadOnlyDictionary<string, IReadOnlyList<AssetInfo>> BuildIndex(
            string assetTypeName,
            string matchFieldPath)
        {
            var assetsByValue = new Dictionary<string, List<AssetInfo>>(StringComparer.Ordinal);

            foreach (AssetInfo asset in _context.GetAssetsByType(assetTypeName))
            {
                AssetField fieldTree = _context.ReadField(asset.PathId);
                AssetField? matchField = AssetFieldNavigator.Find(fieldTree, matchFieldPath);

                if (matchField?.Value is not AssetScalarValue.String stringValue)
                {
                    continue;
                }

                if (!assetsByValue.TryGetValue(stringValue.Value, out var assets))
                {
                    assets = [];
                    assetsByValue.Add(stringValue.Value, assets);
                }

                assets.Add(asset);
            }

            return assetsByValue.ToDictionary(
                static pair => pair.Key,
                static IReadOnlyList<AssetInfo> (pair) => pair.Value.ToArray(),
                StringComparer.Ordinal);
        }
    }
}

public sealed record ReplacementPlanningOutput(
    IReadOnlyList<AssetReplacement> Replacements,
    PatchPreviewResult Preview);
