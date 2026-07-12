using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class AssetQueryService
{
    private readonly IAssetsFileReader _assetsReader;

    public AssetQueryService(IAssetsFileReader assetsReader)
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

    internal static IEnumerable<AssetQueryMatch> FindMatches(
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
            if (!AssetFieldMatcher.MatchesFields(fieldTree, patch.Match))
            {
                continue;
            }

            var ownerMatch = new AssetQueryMatch(asset, fieldTree);

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
            yield return new AssetQueryMatch(componentAsset, componentFieldTree);
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

        return pathIdField?.Value is Int64AssetFieldValue value ? value.Value : null;
    }
}

public sealed record AssetQueryMatch(
    AssetsInfo Asset,
    AssetsFieldInfo FieldTree);

internal sealed class AssetQueryContext
{
    public IReadOnlyDictionary<long, AssetsInfo> AssetsByPathId => _assetsByPathId.Value;

    private IReadOnlyList<AssetsInfo> Assets { get; }

    private readonly IAssetsFileReader _assetsReader;
    private readonly string _assetsFilePath;
    private readonly Lazy<IReadOnlyDictionary<long, AssetsInfo>> _assetsByPathId;

    private readonly Dictionary<string, IReadOnlyList<AssetsInfo>>
        _assetsByType = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<long, AssetsFieldInfo> _fieldTrees = new();

    public AssetQueryContext(IAssetsFileReader assetsReader, string assetsFilePath)
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
        if (_fieldTrees.TryGetValue(pathId, out AssetsFieldInfo? fieldTree))
        {
            return fieldTree;
        }

        fieldTree = _assetsReader.ReadAssetsFieldInfo(_assetsFilePath, pathId);
        _fieldTrees.Add(pathId, fieldTree);

        return fieldTree;
    }
}
