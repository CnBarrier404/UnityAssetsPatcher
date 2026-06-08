using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class FieldPatchPlanBuilder
{
    private readonly AssetQueryService _assetQueryService;
    private readonly FieldPatchOperationPlanner _operationPlanner;

    public FieldPatchPlanBuilder(AssetQueryService assetQueryService, PatchValueResolver valueResolver)
    {
        _assetQueryService = assetQueryService;
        _operationPlanner = new FieldPatchOperationPlanner(valueResolver);
    }

    public PatchPreviewResult CreatePreview(string assetsFilePath, IReadOnlyList<ManifestPatch> targets)
    {
        return new PatchPreviewResult(CreateAssetPlans(assetsFilePath, targets)
            .Select(assetPlan => new PatchPreviewAssetResult(
                assetPlan.Asset,
                assetPlan.Operations
                    .Select(operation => new PatchPreviewOperationResult(
                        operation.Path,
                        operation.OldValue,
                        operation.From,
                        operation.To,
                        operation.WillChange))
                    .ToArray()))
            .ToArray());
    }

    public IReadOnlyList<AssetFieldPatch> CreateWritePlan(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets)
    {
        if (!PatchOperationRules.HasPatchOperations(targets))
        {
            return [];
        }

        var operationGroups = new Dictionary<long, List<FieldPatchOperation>>();

        foreach (FieldPatchAssetPlan assetPlan in CreateAssetPlans(assetsFilePath, targets))
        {
            if (!operationGroups.TryGetValue(assetPlan.Asset.PathId, out var operations))
            {
                operations = [];
                operationGroups.Add(assetPlan.Asset.PathId, operations);
            }

            foreach (FieldPatchOperationPlan operation in assetPlan.Operations)
            {
                FieldPatchWriteOperationMapper.AddTo(operations, operation);
            }
        }

        return operationGroups
            .Select(group => new AssetFieldPatch(group.Key, group.Value))
            .ToArray();
    }

    private IEnumerable<FieldPatchAssetPlan> CreateAssetPlans(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets)
    {
        AssetQueryContext queryContext = _assetQueryService.CreateContext(assetsFilePath);

        foreach (ManifestPatch patch in targets)
        {
            foreach (AssetQueryMatch match in _assetQueryService.FindMatches(queryContext, patch))
            {
                var operations = new List<FieldPatchOperationPlan>();

                foreach (ManifestSetOperation operation in patch.SetOperations ?? [])
                {
                    operations.AddRange(_operationPlanner.CreateSetOperationPlans(
                        assetsFilePath,
                        match.Asset.PathId,
                        match.FieldTree,
                        operation));
                }

                foreach (ManifestAddOperation operation in patch.AddOperations ?? [])
                {
                    operations.AddRange(FieldPatchOperationPlanner.CreateAddOperationPlans(
                        match.Asset.PathId,
                        match.FieldTree,
                        operation));
                }

                yield return new FieldPatchAssetPlan(match.Asset, operations);
            }
        }
    }

    private sealed record FieldPatchAssetPlan(
        AssetsInfo Asset,
        IReadOnlyList<FieldPatchOperationPlan> Operations);
}
