using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed record AssetQueryMatch(AssetInfo Asset, AssetField FieldTree);

internal sealed record AssetQueryPatchMatch(int PatchIndex, AssetQueryMatch Match);

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

    public static IEnumerable<AssetQueryMatch> FindMatches(AssetQueryContext context, ModPatch patch)
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

    internal static IEnumerable<AssetQueryPatchMatch> FindMatches(
        AssetQueryContext context,
        IReadOnlyList<ModPatch> patches)
    {
        var indexedPatches = patches
            .Select((patch, index) => new IndexedPatch(index, patch))
            .ToArray();

        foreach (var patchGroup in indexedPatches.GroupBy(
                     indexedPatch => indexedPatch.Patch.AssetTypeName,
                     StringComparer.OrdinalIgnoreCase))
        {
            var groupedPatches = patchGroup.ToArray();
            var assetsByPathId = groupedPatches
                .Any(indexedPatch => indexedPatch.Patch.ComponentTypeName is not null)
                ? context.AssetsByPathId
                : null;
            var ownerAssets = context.GetAssetsByType(patchGroup.Key);

            foreach (AssetInfo ownerAsset in ownerAssets)
            {
                AssetField ownerFieldTree = context.ReadField(ownerAsset.PathId);
                AssetQueryMatch? ownerMatch = null;
                Dictionary<string, AssetQueryMatch?>? componentMatches = null;

                foreach (IndexedPatch indexedPatch in groupedPatches)
                {
                    ModPatch patch = indexedPatch.Patch;

                    if (!AssetFieldMatcher.MatchesFields(ownerFieldTree, patch.Match))
                    {
                        continue;
                    }

                    if (patch.ComponentTypeName is not { } componentTypeName)
                    {
                        ownerMatch ??= new AssetQueryMatch(ownerAsset, ownerFieldTree);

                        yield return new AssetQueryPatchMatch(indexedPatch.Index, ownerMatch);

                        continue;
                    }

                    componentMatches ??=
                        new Dictionary<string, AssetQueryMatch?>(StringComparer.OrdinalIgnoreCase);

                    if (!componentMatches.TryGetValue(componentTypeName, out AssetQueryMatch? componentMatch))
                    {
                        ownerMatch ??= new AssetQueryMatch(ownerAsset, ownerFieldTree);
                        componentMatch = FindComponentMatch(
                            context,
                            ownerMatch,
                            componentTypeName,
                            assetsByPathId ?? throw new PatchPlanningException(
                                PatchDiagnosticCode.InvalidPatchConfiguration,
                                "Component target index was not initialized."));
                        componentMatches.Add(componentTypeName, componentMatch);
                    }

                    if (componentMatch is not null)
                    {
                        yield return new AssetQueryPatchMatch(indexedPatch.Index, componentMatch);
                    }
                }
            }
        }
    }

    private static IEnumerable<AssetQueryMatch> FindComponentMatches(
        AssetQueryContext context,
        AssetQueryMatch ownerMatch,
        string componentTypeName,
        IReadOnlyDictionary<long, AssetInfo> assetsByPathId)
    {
        AssetQueryMatch? componentMatch = FindComponentMatch(
            context,
            ownerMatch,
            componentTypeName,
            assetsByPathId);

        if (componentMatch is not null)
        {
            yield return componentMatch;
        }
    }

    private static AssetQueryMatch? FindComponentMatch(
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

        switch (componentAssets.Length)
        {
            case > 1:
                throw new PatchPlanningException(
                    PatchDiagnosticCode.InvalidPatchConfiguration,
                    $"GameObject Path ID {ownerMatch.Asset.PathId} contains multiple '{componentTypeName}' components.");
            case 0:
                return null;
        }

        AssetInfo componentAsset = componentAssets[0];
        AssetField componentFieldTree = context.ReadField(componentAsset.PathId);

        return new AssetQueryMatch(componentAsset, componentFieldTree);
    }

    private static long[] ReadComponentPathIds(AssetField gameObjectFieldTree)
    {
        AssetField? componentField = AssetFieldNavigator.Find(gameObjectFieldTree, "m_Component");
        AssetField? arrayField = AssetFieldNavigator.ResolveArray(componentField);

        if (arrayField is null)
        {
            return [];
        }

        return
        [
            .. AssetFieldNavigator.GetArrayElements(arrayField)
                .Select(TryReadComponentPathId)
                .OfType<long>()
                .Where(pathId => pathId != 0)
        ];
    }

    private static long? TryReadComponentPathId(AssetField componentReferenceField)
    {
        AssetField? pathIdField =
            AssetFieldNavigator.Find(componentReferenceField, "component.m_PathID") ??
            AssetFieldNavigator.Find(componentReferenceField, "m_PathID");

        return pathIdField?.Value is AssetFieldValue.Int64 value ? value.Value : null;
    }

    private sealed record IndexedPatch(int Index, ModPatch Patch);
}
