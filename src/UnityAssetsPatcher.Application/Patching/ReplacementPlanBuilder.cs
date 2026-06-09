using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;

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
        string configPath)
    {
        var replacements = new List<AssetReplacement>();

        foreach (ManifestPatch patch in targets)
        {
            if (patch.ReplaceFrom is null)
            {
                continue;
            }

            string sourceAssetsFilePath =
                ResolveReplaceFromAssetsFilePath(configPath, patch.ReplaceFrom.AssetsFilePath);

            foreach (AssetReplacementMatch match in FindReplacementMatches(assetsFilePath, sourceAssetsFilePath,
                         patch))
            {
                replacements.Add(new AssetReplacement(sourceAssetsFilePath, match.Source.PathId,
                    match.Target.PathId));
            }
        }

        return replacements;
    }

    public PatchPreviewResult CreatePreview(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        string configPath)
    {
        var assets = new List<PatchPreviewAssetResult>();

        foreach (ManifestPatch patch in targets)
        {
            if (patch.ReplaceFrom is null)
            {
                continue;
            }

            string sourceAssetsFilePath =
                ResolveReplaceFromAssetsFilePath(configPath, patch.ReplaceFrom.AssetsFilePath);

            foreach (AssetReplacementMatch match in FindReplacementMatches(assetsFilePath, sourceAssetsFilePath,
                         patch))
            {
                var operation = new PatchPreviewOperationResult(
                    "*",
                    $"Path ID {match.Target.PathId}",
                    JsonElementFactory.String(match.MatchValue),
                    JsonElementFactory.String($"Path ID {match.Source.PathId} from {sourceAssetsFilePath}"),
                    true);
                assets.Add(new PatchPreviewAssetResult(match.Target, [operation]));
            }
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

            if (sourceMatches.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Replacement source did not contain a '{patch.AssetTypeName}' asset with {replaceFrom.MatchFieldPath} '{matchValue}'.");
            }

            if (sourceMatches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Replacement source contains multiple '{patch.AssetTypeName}' assets with {replaceFrom.MatchFieldPath} '{matchValue}'.");
            }

            yield return new AssetReplacementMatch(targetMatch.Asset, sourceMatches[0], matchValue);
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

    private static string ResolveReplaceFromAssetsFilePath(string configPath, string assetsFilePath)
    {
        if (Path.IsPathRooted(assetsFilePath))
        {
            return Path.GetFullPath(assetsFilePath);
        }

        string fullConfigPath = Path.GetFullPath(configPath);
        string baseDirectory = Path.GetDirectoryName(fullConfigPath) ?? Directory.GetCurrentDirectory();

        return Path.GetFullPath(Path.Combine(baseDirectory, assetsFilePath));
    }

    private sealed record AssetReplacementMatch(AssetsInfo Target, AssetsInfo Source, string MatchValue);
}
