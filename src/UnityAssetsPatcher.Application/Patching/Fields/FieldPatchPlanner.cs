using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Json;

namespace UnityAssetsPatcher.Application.Patching.Fields;

public sealed class FieldPatchPlanner
{
    private readonly AssetQueryService _assetQueryService;
    private readonly IReadOnlyList<IFieldPatchOperationHandler> _operationHandlers;

    public FieldPatchPlanner(
        AssetQueryService assetQueryService,
        IEnumerable<IFieldPatchOperationHandler> operationHandlers)
    {
        _assetQueryService = assetQueryService;
        _operationHandlers = operationHandlers.ToArray();
    }

    public PatchPreviewResult CreatePreview(string assetsFilePath, IReadOnlyList<ManifestPatch> targets)
    {
        return Plan(assetsFilePath, targets).Preview;
    }

    public IReadOnlyList<AssetFieldPatch> CreateWritePlan(string assetsFilePath, IReadOnlyList<ManifestPatch> targets)
    {
        return Plan(assetsFilePath, targets).Assets;
    }

    public FieldPatchPlanningOutput Plan(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        bool includePreviewDetails = true)
    {
        if (!PatchOperationRules.HasPatchOperations(targets))
        {
            return new FieldPatchPlanningOutput([], new PatchPreviewResult([]));
        }

        var assetPlans = CreateAssetPlans(assetsFilePath, targets).ToArray();
        var operationGroups = new Dictionary<long, List<FieldPatchOperation>>();

        foreach (FieldPatchAssetPlan assetPlan in assetPlans)
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

        var assets = operationGroups
            .Select(group => new AssetFieldPatch(group.Key, group.Value))
            .ToArray();
        var preview = new PatchPreviewResult(includePreviewDetails
            ? assetPlans
                .Select(assetPlan => new PatchPreviewAssetResult(
                    assetPlan.Asset,
                    assetPlan.Operations
                        .Select(operation => new PatchPreviewOperationResult(
                            operation.Path,
                            operation.OldValue,
                            JsonUtils.FormatElementValue(operation.From),
                            JsonUtils.FormatElementValue(operation.To),
                            operation.WillChange))
                        .ToArray()))
                .ToArray()
            : []);

        return new FieldPatchPlanningOutput(assets, preview);
    }

    private IEnumerable<FieldPatchAssetPlan> CreateAssetPlans(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets)
    {
        AssetQueryContext queryContext = _assetQueryService.CreateContext(assetsFilePath);

        foreach (ManifestPatch patch in targets)
        {
            var normalizedOperations =
                NormalizeOperations(queryContext, assetsFilePath, patch);

            foreach (AssetQueryMatch match in AssetQueryService.FindMatches(queryContext, patch))
            {
                var operations = new List<FieldPatchOperationPlan>();

                foreach (NormalizedFieldPatchOperation operation in normalizedOperations)
                {
                    IFieldPatchOperationHandler handler = GetOperationHandler(operation);
                    operations.AddRange(handler.CreatePlans(
                        match.Asset.PathId, match.FieldTree, operation));
                }

                yield return new FieldPatchAssetPlan(match.Asset, operations);
            }
        }
    }

    private IFieldPatchOperationHandler GetOperationHandler(NormalizedFieldPatchOperation operation)
    {
        var matches = _operationHandlers
            .Where(candidate => candidate.CanHandle(operation))
            .Take(2)
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                $"No field patch operation handler is registered for '{operation.GetType().Name}'."),
            _ => throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                $"Multiple field patch operation handlers are registered for '{operation.GetType().Name}'."),
        };
    }

    private static NormalizedFieldPatchOperation[] NormalizeOperations(
        AssetQueryContext queryContext,
        string assetsFilePath,
        ManifestPatch patch)
    {
        IEnumerable<NormalizedFieldPatchOperation> setOperations = (patch.SetOperations ?? [])
            .Select(operation => new NormalizedSetFieldPatchOperation(
                new Lazy<ManifestSetOperation>(() => PatchValueResolver.ResolveSetOperation(
                    queryContext, assetsFilePath, operation))));

        IEnumerable<NormalizedFieldPatchOperation> addOperations = (patch.AddOperations ?? [])
            .Select(operation => new NormalizedAddFieldPatchOperation(operation));

        return setOperations.Concat(addOperations).ToArray();
    }

    private sealed record FieldPatchAssetPlan(AssetInfo Asset, IReadOnlyList<FieldPatchOperationPlan> Operations);
}

public sealed record FieldPatchPlanningOutput(IReadOnlyList<AssetFieldPatch> Assets, PatchPreviewResult Preview);

public abstract record NormalizedFieldPatchOperation;

public sealed record NormalizedSetFieldPatchOperation(Lazy<ManifestSetOperation> Operation)
    : NormalizedFieldPatchOperation;

public sealed record NormalizedAddFieldPatchOperation(ManifestAddOperation Operation)
    : NormalizedFieldPatchOperation;

public interface IFieldPatchOperationHandler
{
    public bool CanHandle(NormalizedFieldPatchOperation operation);

    public IReadOnlyList<FieldPatchOperationPlan> CreatePlans(
        long pathId,
        AssetField fieldTree,
        NormalizedFieldPatchOperation operation);
}

public sealed class SetFieldPatchOperationHandler : IFieldPatchOperationHandler
{
    public bool CanHandle(NormalizedFieldPatchOperation operation)
    {
        return operation is NormalizedSetFieldPatchOperation;
    }

    public IReadOnlyList<FieldPatchOperationPlan> CreatePlans(
        long pathId,
        AssetField fieldTree,
        NormalizedFieldPatchOperation operation)
    {
        var set = (NormalizedSetFieldPatchOperation)operation;
        return FieldPatchOperationPlanner.CreateSetOperationPlans(pathId, fieldTree, set.Operation.Value);
    }
}

public sealed class AddFieldPatchOperationHandler : IFieldPatchOperationHandler
{
    public bool CanHandle(NormalizedFieldPatchOperation operation)
    {
        return operation is NormalizedAddFieldPatchOperation;
    }

    public IReadOnlyList<FieldPatchOperationPlan> CreatePlans(
        long pathId,
        AssetField fieldTree,
        NormalizedFieldPatchOperation operation)
    {
        var add = (NormalizedAddFieldPatchOperation)operation;
        return FieldPatchOperationPlanner.CreateAddOperationPlans(pathId, fieldTree, add.Operation);
    }
}
