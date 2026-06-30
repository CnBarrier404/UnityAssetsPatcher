using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Modules.Patching;

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
        var replacements = new List<AssetReplacement>();

        foreach (ManifestPatch patch in targets)
        {
            if (patch.ReplaceFrom is null)
            {
                continue;
            }

            string sourceAssetsFilePath =
                ResolveReplaceFromAssetsFilePath(sourceAssetsPaths, patch.ReplaceFrom.AssetsFilePath);

            replacements.AddRange(FindReplacementMatches(assetsFilePath, sourceAssetsFilePath, patch).Select(match =>
                new AssetReplacement(sourceAssetsFilePath, match.Source.PathId, match.Target.PathId)));
        }

        return replacements;
    }

    public PatchPreviewResult CreatePreview(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        IReadOnlyDictionary<string, string> sourceAssetsPaths)
    {
        var assets = new List<PatchPreviewAssetResult>();

        foreach (ManifestPatch patch in targets)
        {
            if (patch.ReplaceFrom is null)
            {
                continue;
            }

            string sourceAssetsFilePath =
                ResolveReplaceFromAssetsFilePath(sourceAssetsPaths, patch.ReplaceFrom.AssetsFilePath);

            assets.AddRange(from match in FindReplacementMatches(assetsFilePath, sourceAssetsFilePath, patch)
                let operation = new PatchPreviewOperationResult("*", $"Path ID {match.Target.PathId}",
                    JsonElementFactory.String(match.MatchValue),
                    JsonElementFactory.String($"Path ID {match.Source.PathId} from {sourceAssetsFilePath}"),
                    match.MatchValue, $"Path ID {match.Source.PathId} from {sourceAssetsFilePath}", true)
                select new PatchPreviewAssetResult(match.Target, [operation]));
        }

        return new PatchPreviewResult(assets);
    }

    private IEnumerable<AssetReplacementMatch> FindReplacementMatches(
        string targetAssetsFilePath,
        string sourceAssetsFilePath,
        ManifestPatch patch)
    {
        ManifestReplaceFrom replaceFrom = patch.ReplaceFrom ??
                                          throw new InvalidOperationException(
                                              "Replacement patch is missing replaceFrom.");
        var seenTargetValues = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (AssetQueryMatch targetMatch in _assetQueryService.FindMatches(targetAssetsFilePath, patch))
        {
            string matchValue = ReadReplacementMatchValue(targetMatch.FieldTree, replaceFrom.MatchFieldPath,
                targetMatch.Asset.PathId, "target");

            if (!seenTargetValues.TryAdd(matchValue, targetMatch.Asset.PathId))
            {
                throw new InvalidOperationException(
                    $"Replacement target contains multiple '{patch.AssetTypeName}' assets with {replaceFrom.MatchFieldPath} '{matchValue}'.");
            }

            var sourceIncludeGroup = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [replaceFrom.MatchFieldPath] = JsonElementFactory.String(matchValue),
            };
            var sourcePatch = new ManifestPatch(
                Path.GetFileName(sourceAssetsFilePath),
                patch.AssetTypeName,
                [sourceIncludeGroup],
                null,
                null);
            var sourceMatches = _assetQueryService.FindMatches(sourceAssetsFilePath, sourcePatch)
                .Select(match => match.Asset)
                .ToArray();

            yield return sourceMatches.Length switch
            {
                0 => throw new InvalidOperationException(
                    $"Replacement source did not contain a '{patch.AssetTypeName}' asset with {replaceFrom.MatchFieldPath} '{matchValue}'."),
                > 1 => throw new InvalidOperationException(
                    $"Replacement source contains multiple '{patch.AssetTypeName}' assets with {replaceFrom.MatchFieldPath} '{matchValue}'."),
                _ => new AssetReplacementMatch(targetMatch.Asset, sourceMatches[0], matchValue)
            };
        }
    }

    private static string ReadReplacementMatchValue(
        AssetsFieldInfo fieldTree,
        string matchFieldPath,
        long pathId,
        string role)
    {
        AssetsFieldInfo? field = AssetFieldNavigator.FindField(fieldTree, matchFieldPath);

        return field?.Value ?? throw new InvalidOperationException(
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

    private sealed record AssetReplacementMatch(AssetsInfo Target, AssetsInfo Source, string MatchValue);
}
