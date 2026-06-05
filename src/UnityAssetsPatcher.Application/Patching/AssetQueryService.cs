using System.Globalization;
using System.Text.Json;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class AssetQueryService
{
    private readonly IAssetsReader _assetsReader;

    public AssetQueryService(IAssetsReader assetsReader)
    {
        _assetsReader = assetsReader;
    }

    public IReadOnlyList<AssetMatch> FindAssetMatches(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets)
    {
        var matches = new List<AssetMatch>();

        foreach (ManifestPatch patch in targets)
        {
            foreach (AssetQueryMatch match in FindMatches(assetsFilePath, patch))
            {
                matches.Add(new AssetMatch(match.Asset, match.IncludeGroup));
            }
        }

        return matches;
    }

    public IEnumerable<AssetQueryMatch> FindMatches(
        string assetsFilePath,
        ManifestPatch patch)
    {
        var assets = _assetsReader.ReadAssetsInfo(assetsFilePath).ToArray();
        var assetsByPathId = patch.ComponentTypeName is null
            ? null
            : assets.ToDictionary(asset => asset.PathId);
        var ownerAssets = assets
            .Where(asset => string.Equals(asset.TypeName, patch.AssetTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (AssetsInfo asset in ownerAssets)
        {
            AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(assetsFilePath, asset.PathId);
            var includeGroup =
                patch.IncludeGroups.FirstOrDefault(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));

            if (includeGroup is null)
            {
                continue;
            }

            var ownerMatch = new AssetQueryMatch(asset, fieldTree, includeGroup);

            if (patch.ComponentTypeName is not string componentTypeName)
            {
                yield return ownerMatch;
                continue;
            }

            IReadOnlyDictionary<long, AssetsInfo> componentAssetsByPathId = assetsByPathId ??
                                                                            throw new InvalidOperationException(
                                                                                "Component target index was not initialized.");
            foreach (AssetQueryMatch componentMatch in FindComponentMatches(
                         assetsFilePath,
                         ownerMatch,
                         componentTypeName,
                         componentAssetsByPathId))
            {
                yield return componentMatch;
            }
        }
    }

    private IEnumerable<AssetQueryMatch> FindComponentMatches(
        string assetsFilePath,
        AssetQueryMatch ownerMatch,
        string componentTypeName,
        IReadOnlyDictionary<long, AssetsInfo> assetsByPathId)
    {
        var componentAssets = ReadComponentPathIds(ownerMatch.FieldTree)
            .Select(pathId => assetsByPathId.TryGetValue(pathId, out AssetsInfo? asset) ? asset : null)
            .OfType<AssetsInfo>()
            .Where(asset => string.Equals(asset.TypeName, componentTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (componentAssets.Length > 1)
        {
            throw new InvalidOperationException(
                $"GameObject Path ID {ownerMatch.Asset.PathId} contains multiple '{componentTypeName}' components.");
        }

        foreach (AssetsInfo componentAsset in componentAssets)
        {
            AssetsFieldInfo componentFieldTree =
                _assetsReader.ReadAssetsFieldInfo(assetsFilePath, componentAsset.PathId);
            yield return new AssetQueryMatch(componentAsset, componentFieldTree, ownerMatch.IncludeGroup);
        }
    }

    private static IReadOnlyList<long> ReadComponentPathIds(AssetsFieldInfo gameObjectFieldTree)
    {
        AssetsFieldInfo? componentField = AssetFieldMatcher.FindField(gameObjectFieldTree, "m_Component");
        AssetsFieldInfo? arrayField = PatchFieldValueFormatter.ResolveArrayField(componentField);

        if (arrayField is null)
        {
            return [];
        }

        return PatchFieldValueFormatter.GetArrayElementFields(arrayField)
            .Select(TryReadComponentPathId)
            .OfType<long>()
            .Where(pathId => pathId != 0)
            .ToArray();
    }

    private static long? TryReadComponentPathId(AssetsFieldInfo componentReferenceField)
    {
        AssetsFieldInfo? pathIdField =
            AssetFieldMatcher.FindField(componentReferenceField, "component.m_PathID") ??
            AssetFieldMatcher.FindField(componentReferenceField, "m_PathID");

        return long.TryParse(pathIdField?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long pathId)
            ? pathId
            : null;
    }
}

public sealed record AssetQueryMatch(
    AssetsInfo Asset,
    AssetsFieldInfo FieldTree,
    IReadOnlyDictionary<string, JsonElement> IncludeGroup);
