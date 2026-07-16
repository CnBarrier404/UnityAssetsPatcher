using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class ReplacementPlanBuilder
{
    private readonly AssetQueryService _assetQueryService;

    public ReplacementPlanBuilder(AssetQueryService assetQueryService)
    {
        _assetQueryService = assetQueryService;
    }

    public IReadOnlyList<AssetReplacement> CreateWritePlan(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        IReadOnlyDictionary<string, string> sourceAssetsPaths)
    {
        return FindReplacementMatches(assetsFilePath, targets, sourceAssetsPaths)
            .Select(match => new AssetReplacement(
                match.SourceAssetsFilePath,
                match.Source.PathId,
                match.Target.PathId))
            .ToArray();
    }

    public PatchPreviewResult CreatePreview(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        IReadOnlyDictionary<string, string> sourceAssetsPaths)
    {
        var assets = FindReplacementMatches(assetsFilePath, targets, sourceAssetsPaths)
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

        return new PatchPreviewResult(assets);
    }

    private IEnumerable<AssetReplacementMatch> FindReplacementMatches(
        string targetAssetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        IReadOnlyDictionary<string, string> sourceAssetsPaths)
    {
        AssetQueryContext? targetContext = null;
        var sourceIndexesByPath =
            new Dictionary<string, ReplacementSourceIndexes>(StringComparer.OrdinalIgnoreCase);

        foreach (ManifestPatch patch in targets)
        {
            if (patch.ReplaceFrom is not { } replaceFrom)
            {
                continue;
            }

            string sourceAssetsFilePath =
                ResolveReplaceFromAssetsFilePath(sourceAssetsPaths, replaceFrom.AssetsFilePath);
            var seenTargetValues = new Dictionary<string, long>(StringComparer.Ordinal);
            targetContext ??= _assetQueryService.CreateContext(targetAssetsFilePath);

            foreach (AssetQueryMatch targetMatch in AssetQueryService.FindMatches(targetContext, patch))
            {
                string matchValue = ReadReplacementMatchValue(
                    targetMatch.FieldTree,
                    replaceFrom.MatchFieldPath,
                    targetMatch.Asset.PathId,
                    "target");

                if (!seenTargetValues.TryAdd(matchValue, targetMatch.Asset.PathId))
                {
                    throw new InvalidOperationException(
                        $"Replacement target contains multiple '{patch.AssetTypeName}' assets with {replaceFrom.MatchFieldPath} '{matchValue}'.");
                }

                if (!sourceIndexesByPath.TryGetValue(sourceAssetsFilePath, out ReplacementSourceIndexes? indexes))
                {
                    indexes = new ReplacementSourceIndexes(
                        _assetQueryService.CreateContext(sourceAssetsFilePath));
                    sourceIndexesByPath.Add(sourceAssetsFilePath, indexes);
                }

                var sourceMatches = indexes
                    .GetIndex(patch.AssetTypeName, replaceFrom.MatchFieldPath)
                    .GetValueOrDefault(matchValue, []);

                yield return sourceMatches.Count switch
                {
                    0 => throw new InvalidOperationException(
                        $"Replacement source did not contain a '{patch.AssetTypeName}' asset with {replaceFrom.MatchFieldPath} '{matchValue}'."),
                    > 1 => throw new InvalidOperationException(
                        $"Replacement source contains multiple '{patch.AssetTypeName}' assets with {replaceFrom.MatchFieldPath} '{matchValue}'."),
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
        AssetsFieldInfo fieldTree,
        string matchFieldPath,
        long pathId,
        string role)
    {
        AssetsFieldInfo? field = AssetFieldNavigator.FindField(fieldTree, matchFieldPath);

        return field?.Value?.ToInvariantString() ?? throw new InvalidOperationException(
            $"Replacement {role} Path ID {pathId} does not contain scalar match field '{matchFieldPath}'.");
    }

    private static string ResolveReplaceFromAssetsFilePath(
        IReadOnlyDictionary<string, string> sourceAssetsPaths,
        string assetsFilePath)
    {
        string normalizedPath = assetsFilePath.Replace('\\', '/');

        if (sourceAssetsPaths.TryGetValue(normalizedPath, out string? resolvedPath))
        {
            return resolvedPath;
        }

        throw new FileNotFoundException(
            $"Replacement source assets file not found in package: {assetsFilePath}");
    }

    private sealed record AssetReplacementMatch(
        AssetsInfo Target,
        AssetsInfo Source,
        string MatchValue,
        string SourceAssetsFilePath);

    private sealed class ReplacementSourceIndexes
    {
        private readonly AssetQueryContext _context;

        private readonly Dictionary<string, Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<AssetsInfo>>>>
            _indexesByType = new(StringComparer.OrdinalIgnoreCase);

        public ReplacementSourceIndexes(AssetQueryContext context)
        {
            _context = context;
        }

        public IReadOnlyDictionary<string, IReadOnlyList<AssetsInfo>> GetIndex(
            string assetTypeName,
            string matchFieldPath)
        {
            if (!_indexesByType.TryGetValue(assetTypeName, out var indexesByFieldPath))
            {
                indexesByFieldPath = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<AssetsInfo>>>(
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

        private IReadOnlyDictionary<string, IReadOnlyList<AssetsInfo>> BuildIndex(
            string assetTypeName,
            string matchFieldPath)
        {
            var assetsByValue = new Dictionary<string, List<AssetsInfo>>(StringComparer.Ordinal);

            foreach (AssetsInfo asset in _context.GetAssetsByType(assetTypeName))
            {
                AssetsFieldInfo fieldTree = _context.ReadAssetsFieldInfo(asset.PathId);
                AssetsFieldInfo? matchField = AssetFieldNavigator.FindField(fieldTree, matchFieldPath);

                if (matchField?.Value is not StringAssetFieldValue stringValue)
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
                static IReadOnlyList<AssetsInfo> (pair) => pair.Value.ToArray(),
                StringComparer.Ordinal);
        }
    }
}
