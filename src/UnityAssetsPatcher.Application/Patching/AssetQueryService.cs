using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Patching;

public sealed record AssetQueryMatch(AssetInfo Asset, AssetField FieldTree);

public sealed class AssetQueryService
{
    private readonly IAssetsFileReader _assetsReader;

    public AssetQueryService(IAssetsFileReader assetsReader)
    {
        _assetsReader = assetsReader;
    }

    public AssetQueryContext CreateContext(string assetsFilePath)
    {
        return new AssetQueryContext(_assetsReader, assetsFilePath);
    }

    public static IEnumerable<AssetQueryMatch> FindMatches(
        AssetQueryContext context,
        ManifestPatch patch)
    {
        var assetsByPathId = patch.ComponentTypeName is null
            ? null
            : context.AssetsByPathId;

        var ownerAssets = context.GetAssetsByType(patch.AssetTypeName);

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

            var componentAssetsByPathId = assetsByPathId ??
                                          throw new PatchPlanningException(
                                              PatchDiagnosticCode.InvalidPatchConfiguration,
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
        var componentAssets = ReadComponentPathIds(ownerMatch.FieldTree)
            .Select(assetsByPathId.GetValueOrDefault)
            .OfType<AssetInfo>()
            .Where(asset => string.Equals(asset.TypeName, componentTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (componentAssets.Length > 1)
        {
            throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                $"GameObject Path ID {ownerMatch.Asset.PathId} contains multiple '{componentTypeName}' components.");
        }

        foreach (AssetInfo componentAsset in componentAssets)
        {
            AssetField componentFieldTree = context.ReadField(componentAsset.PathId);

            yield return new AssetQueryMatch(componentAsset, componentFieldTree);
        }
    }

    private static long[] ReadComponentPathIds(AssetField gameObjectFieldTree)
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
