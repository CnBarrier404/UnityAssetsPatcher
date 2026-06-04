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
        var assets = _assetsReader.ReadAssetsInfo(assetsFilePath)
            .Where(asset => string.Equals(asset.TypeName, patch.AssetTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (AssetsInfo asset in assets)
        {
            AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(assetsFilePath, asset.PathId);
            IReadOnlyDictionary<string, JsonElement>? includeGroup =
                patch.IncludeGroups.FirstOrDefault(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));

            if (includeGroup is not null)
            {
                yield return new AssetQueryMatch(asset, fieldTree, includeGroup);
            }
        }
    }
}

public sealed record AssetQueryMatch(
    AssetsInfo Asset,
    AssetsFieldInfo FieldTree,
    IReadOnlyDictionary<string, JsonElement> IncludeGroup);
