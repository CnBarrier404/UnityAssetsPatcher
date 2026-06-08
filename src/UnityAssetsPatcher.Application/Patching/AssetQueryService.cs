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

    public IEnumerable<AssetQueryMatch> FindMatches(
        string assetsFilePath,
        ManifestPatch patch)
    {
        return FindMatches(CreateContext(assetsFilePath), patch);
    }

    internal AssetQueryContext CreateContext(string assetsFilePath)
    {
        return new AssetQueryContext(_assetsReader, assetsFilePath);
    }

    internal IEnumerable<AssetQueryMatch> FindMatches(
        AssetQueryContext context,
        ManifestPatch patch)
    {
        var assetsByPathId = patch.ComponentTypeName is null
            ? null
            : context.AssetsByPathId;
        var ownerAssets = context.GetAssetsByType(patch.AssetTypeName);

        foreach (AssetsInfo asset in ownerAssets)
        {
            AssetsFieldInfo fieldTree = context.ReadAssetsFieldInfo(asset.PathId);
            var includeGroup =
                patch.IncludeGroups.FirstOrDefault(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));

            if (includeGroup is null)
            {
                continue;
            }

            var ownerMatch = new AssetQueryMatch(asset, fieldTree, includeGroup);

            if (patch.ComponentTypeName is not { } componentTypeName)
            {
                yield return ownerMatch;
                continue;
            }

            var componentAssetsByPathId = assetsByPathId ??
                                          throw new InvalidOperationException(
                                              "Component target index was not initialized.");
            foreach (AssetQueryMatch componentMatch in FindComponentMatches(
                         context,
                         ownerMatch,
                         componentTypeName,
                         componentAssetsByPathId))
            {
                yield return componentMatch;
            }
        }
    }

    private static IEnumerable<AssetQueryMatch> FindComponentMatches(
        AssetQueryContext context,
        AssetQueryMatch ownerMatch,
        string componentTypeName,
        IReadOnlyDictionary<long, AssetsInfo> assetsByPathId)
    {
        var componentAssets = ReadComponentPathIds(ownerMatch.FieldTree)
            .Select(assetsByPathId.GetValueOrDefault)
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
                context.ReadAssetsFieldInfo(componentAsset.PathId);
            yield return new AssetQueryMatch(componentAsset, componentFieldTree, ownerMatch.IncludeGroup);
        }
    }

    private static IReadOnlyList<long> ReadComponentPathIds(AssetsFieldInfo gameObjectFieldTree)
    {
        AssetsFieldInfo? componentField = AssetFieldNavigator.FindField(gameObjectFieldTree, "m_Component");
        AssetsFieldInfo? arrayField = AssetFieldNavigator.ResolveArrayField(componentField);

        if (arrayField is null)
        {
            return [];
        }

        return AssetFieldNavigator.GetArrayElementFields(arrayField)
            .Select(TryReadComponentPathId)
            .OfType<long>()
            .Where(pathId => pathId != 0)
            .ToArray();
    }

    private static long? TryReadComponentPathId(AssetsFieldInfo componentReferenceField)
    {
        AssetsFieldInfo? pathIdField =
            AssetFieldNavigator.FindField(componentReferenceField, "component.m_PathID") ??
            AssetFieldNavigator.FindField(componentReferenceField, "m_PathID");

        return long.TryParse(pathIdField?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long pathId)
            ? pathId
            : null;
    }
}

public sealed record AssetQueryMatch(
    AssetsInfo Asset,
    AssetsFieldInfo FieldTree,
    IReadOnlyDictionary<string, JsonElement> IncludeGroup);

internal sealed class AssetQueryContext
{
    public IReadOnlyDictionary<long, AssetsInfo> AssetsByPathId => _assetsByPathId.Value;

    private IReadOnlyList<AssetsInfo> Assets { get; }

    private readonly IAssetsReader _assetsReader;
    private readonly string _assetsFilePath;
    private readonly Lazy<IReadOnlyDictionary<long, AssetsInfo>> _assetsByPathId;

    private readonly Dictionary<string, IReadOnlyList<AssetsInfo>>
        _assetsByType = new(StringComparer.OrdinalIgnoreCase);

    public AssetQueryContext(IAssetsReader assetsReader, string assetsFilePath)
    {
        _assetsReader = assetsReader;
        _assetsFilePath = assetsFilePath;
        Assets = assetsReader.ReadAssetsInfo(assetsFilePath).ToArray();
        _assetsByPathId =
            new Lazy<IReadOnlyDictionary<long, AssetsInfo>>(() => Assets.ToDictionary(asset => asset.PathId));
    }

    public IReadOnlyList<AssetsInfo> GetAssetsByType(string assetTypeName)
    {
        if (_assetsByType.TryGetValue(assetTypeName, out var assets))
        {
            return assets;
        }

        assets = Assets
            .Where(asset => string.Equals(asset.TypeName, assetTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _assetsByType.Add(assetTypeName, assets);

        return assets;
    }

    public AssetsFieldInfo ReadAssetsFieldInfo(long pathId)
    {
        return _assetsReader.ReadAssetsFieldInfo(_assetsFilePath, pathId);
    }
}
