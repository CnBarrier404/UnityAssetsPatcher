using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public sealed class AssetsWorkflowService
{
    private readonly IAssetsReader _assetsReader;

    public AssetsWorkflowService(IAssetsReader assetsReader)
    {
        _assetsReader = assetsReader;
    }

    public IReadOnlyList<AssetsInfo> InspectList(InspectListRequest request)
    {
        return _assetsReader.ReadAssetsInfo(request.AssetsFilePath);
    }

    public AssetsFieldInfo InspectFields(InspectFieldsRequest request)
    {
        return _assetsReader.ReadAssetsFieldInfo(request.AssetsFilePath, request.PathId);
    }

    public IReadOnlyList<AssetMatch> FindAssets(FindAssetsRequest request)
    {
        AssetQueryConfig queryConfig = AssetQueryConfigLoader.Load(request.ConfigPath);
        var matches = new List<AssetMatch>();

        foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(request.AssetsFilePath, queryConfig))
        {
            var includeGroup = queryConfig.IncludeGroups
                .First(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));
            matches.Add(new AssetMatch(asset, includeGroup));
        }

        return matches;
    }

    public PatchPreviewResult PreviewPatch(PatchPreviewRequest request)
    {
        AssetQueryConfig queryConfig = AssetQueryConfigLoader.Load(request.ConfigPath);

        if (queryConfig.SetOperations is null || queryConfig.SetOperations.Count == 0)
        {
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' array.");
        }

        var assets = new List<PatchPreviewAssetResult>();

        foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(request.AssetsFilePath, queryConfig))
        {
            var operationResults = new List<PatchPreviewOperationResult>();

            foreach (PatchSetOperation operation in queryConfig.SetOperations)
            {
                AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.Path);
                string oldValue = field?.Value ?? "<missing>";
                bool matches = field?.Value is not null && AssetFieldMatcher.MatchesValue(field.Value, operation.From);

                operationResults.Add(new PatchPreviewOperationResult(
                    operation.Path,
                    oldValue,
                    operation.From,
                    operation.To,
                    matches));
            }

            assets.Add(new PatchPreviewAssetResult(asset, operationResults));
        }

        return new PatchPreviewResult(assets);
    }

    private IEnumerable<(AssetsInfo Asset, AssetsFieldInfo FieldTree)> FindMatchingAssets(string assetsFilePath,
        AssetQueryConfig queryConfig)
    {
        var assets = _assetsReader.ReadAssetsInfo(assetsFilePath)
            .Where(asset => string.Equals(asset.TypeName, queryConfig.Type, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (AssetsInfo asset in assets)
        {
            AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(assetsFilePath, asset.PathId);
            bool matches = queryConfig.IncludeGroups.Any(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));

            if (matches)
            {
                yield return (asset, fieldTree);
            }
        }
    }
}

public sealed record InspectListRequest(string AssetsFilePath, int? Limit);

public sealed record InspectFieldsRequest(string AssetsFilePath, long PathId);

public sealed record FindAssetsRequest(string AssetsFilePath, string ConfigPath);

public sealed record PatchPreviewRequest(string AssetsFilePath, string ConfigPath);

public sealed record AssetMatch(AssetsInfo Asset, IReadOnlyDictionary<string, JsonElement> IncludeGroup);

public sealed record PatchPreviewResult(IReadOnlyList<PatchPreviewAssetResult> Assets);

public sealed record PatchPreviewAssetResult(AssetsInfo Asset, IReadOnlyList<PatchPreviewOperationResult> Operations);

public sealed record PatchPreviewOperationResult(
    string Path,
    string OldValue,
    JsonElement From,
    JsonElement To,
    bool WillChange);
