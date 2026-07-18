using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;

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

    public AssetQueryContext CreateContext(string assetsFilePath)
    {
        return new AssetQueryContext(_assetsReader, assetsFilePath);
    }

    public static IEnumerable<AssetQueryMatch> FindMatches(
        AssetQueryContext context,
        ManifestPatch patch)
    {
        IReadOnlyDictionary<long, AssetInfo>? assetsByPathId = patch.ComponentTypeName is null
            ? null
            : context.AssetsByPathId;
        IReadOnlyList<AssetInfo> ownerAssets = context.GetAssetsByType(patch.AssetTypeName);

        foreach (AssetInfo asset in ownerAssets)
        {
            AssetField fieldTree = context.ReadField(asset.PathId);

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

            IReadOnlyDictionary<long, AssetInfo> componentAssetsByPathId = assetsByPathId ??
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
        IReadOnlyDictionary<long, AssetInfo> assetsByPathId)
    {
        AssetInfo[] componentAssets = ReadComponentPathIds(ownerMatch.FieldTree)
            .Select(assetsByPathId.GetValueOrDefault)
            .OfType<AssetInfo>()
            .Where(asset => string.Equals(asset.TypeName, componentTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (componentAssets.Length > 1)
        {
            throw new InvalidOperationException(
                $"GameObject Path ID {ownerMatch.Asset.PathId} contains multiple '{componentTypeName}' components.");
        }

        foreach (AssetInfo componentAsset in componentAssets)
        {
            AssetField componentFieldTree = context.ReadField(componentAsset.PathId);

            yield return new AssetQueryMatch(componentAsset, componentFieldTree);
        }
    }

    private static IReadOnlyList<long> ReadComponentPathIds(AssetField gameObjectFieldTree)
    {
        AssetField? componentField = AssetFieldNavigator.Find(gameObjectFieldTree, "m_Component");
        AssetField? arrayField = AssetFieldNavigator.ResolveArray(componentField);

        if (arrayField is null)
        {
            return [];
        }

        return AssetFieldNavigator.GetArrayElements(arrayField)
            .Select(TryReadComponentPathId)
            .OfType<long>()
            .Where(pathId => pathId != 0)
            .ToArray();
    }

    private static long? TryReadComponentPathId(AssetField componentReferenceField)
    {
        AssetField? pathIdField =
            AssetFieldNavigator.Find(componentReferenceField, "component.m_PathID") ??
            AssetFieldNavigator.Find(componentReferenceField, "m_PathID");

        return pathIdField?.Value is AssetFieldValue.Int64 value ? value.Value : null;
    }
}

public sealed record AssetQueryMatch(
    AssetInfo Asset,
    AssetField FieldTree);

public sealed class AssetQueryContext
{
    public IReadOnlyDictionary<long, AssetInfo> AssetsByPathId => _assetsByPathId.Value;

    private IReadOnlyList<AssetInfo> Assets { get; }

    private readonly IAssetsFileReader _assetsReader;
    private readonly string _assetsFilePath;
    private readonly Lazy<IReadOnlyDictionary<long, AssetInfo>> _assetsByPathId;

    private readonly Dictionary<string, IReadOnlyList<AssetInfo>>
        _assetsByType = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<long, AssetField> _fieldTrees = new();

    public AssetQueryContext(IAssetsFileReader assetsReader, string assetsFilePath)
    {
        _assetsReader = assetsReader;
        _assetsFilePath = assetsFilePath;
        Assets = assetsReader.ReadAssets(assetsFilePath).ToArray();
        _assetsByPathId =
            new Lazy<IReadOnlyDictionary<long, AssetInfo>>(() => Assets.ToDictionary(asset => asset.PathId));
    }

    public IReadOnlyList<AssetInfo> GetAssetsByType(string assetTypeName)
    {
        if (_assetsByType.TryGetValue(assetTypeName, out IReadOnlyList<AssetInfo>? assets))
        {
            return assets;
        }

        assets = Assets
            .Where(asset => string.Equals(asset.TypeName, assetTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _assetsByType.Add(assetTypeName, assets);

        return assets;
    }

    public AssetField ReadField(long pathId)
    {
        if (_fieldTrees.TryGetValue(pathId, out AssetField? fieldTree))
        {
            return fieldTree;
        }

        fieldTree = _assetsReader.ReadField(_assetsFilePath, pathId);
        _fieldTrees.Add(pathId, fieldTree);

        return fieldTree;
    }
}
