using System.Text.Json;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class FieldPatchPlanBuilder
{
    private readonly AssetQueryService _assetQueryService;
    private readonly PatchValueResolver _valueResolver;

    public FieldPatchPlanBuilder(AssetQueryService assetQueryService, PatchValueResolver valueResolver)
    {
        _assetQueryService = assetQueryService;
        _valueResolver = valueResolver;
    }

    public PatchPreviewResult CreatePreview(string assetsFilePath, IReadOnlyList<ManifestPatch> targets)
    {
        var assets = new List<PatchPreviewAssetResult>();
        AssetQueryContext queryContext = _assetQueryService.CreateContext(assetsFilePath);

        foreach (ManifestPatch patch in targets)
        {
            foreach (AssetQueryMatch match in _assetQueryService.FindMatches(queryContext, patch))
            {
                var operationResults = new List<PatchPreviewOperationResult>();

                foreach (ManifestSetOperation operation in patch.SetOperations ?? [])
                {
                    operationResults.AddRange(CreatePatchPreviewOperationResults(
                        assetsFilePath,
                        match.FieldTree,
                        operation));
                }

                foreach (ManifestAddOperation operation in patch.AddOperations ?? [])
                {
                    operationResults.AddRange(CreatePatchPreviewOperationResults(match.FieldTree, operation));
                }

                assets.Add(new PatchPreviewAssetResult(match.Asset, operationResults));
            }
        }

        return new PatchPreviewResult(assets);
    }

    public IReadOnlyList<PatchWriteAsset> CreateWritePlan(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets)
    {
        if (!PatchOperationRules.HasPatchOperations(targets))
        {
            return [];
        }

        var operationGroups = new Dictionary<long, List<PatchWriteOperation>>();
        AssetQueryContext queryContext = _assetQueryService.CreateContext(assetsFilePath);

        foreach (ManifestPatch patch in targets)
        {
            foreach (AssetQueryMatch match in _assetQueryService.FindMatches(queryContext, patch))
            {
                if (!operationGroups.TryGetValue(match.Asset.PathId, out var operations))
                {
                    operations = [];
                    operationGroups.Add(match.Asset.PathId, operations);
                }

                foreach (ManifestSetOperation operation in patch.SetOperations ?? [])
                {
                    operations.AddRange(CreatePatchWriteOperations(
                        assetsFilePath,
                        match.Asset.PathId,
                        match.FieldTree,
                        operation));
                }

                foreach (ManifestAddOperation operation in patch.AddOperations ?? [])
                {
                    operations.AddRange(CreatePatchWriteOperations(match.Asset.PathId, match.FieldTree, operation));
                }
            }
        }

        return operationGroups
            .Select(group => new PatchWriteAsset(group.Key, group.Value))
            .ToArray();
    }

    private IReadOnlyList<PatchPreviewOperationResult> CreatePatchPreviewOperationResults(
        string assetsFilePath,
        AssetsFieldInfo fieldTree,
        ManifestSetOperation operation)
    {
        operation = _valueResolver.ResolveSetOperation(assetsFilePath, operation);
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.FieldPath);

        if (!JsonUtils.TryGetObjectValue(operation.To, out JsonElement toObject))
        {
            string path = operation.FieldPath;
            string oldValue = field?.Value ?? "<missing>";

            if (PatchFieldValueFormatter.IsJsonArrayPatchValue(operation.To))
            {
                AssetsFieldInfo? arrayField = PatchFieldValueFormatter.ResolveArrayField(field);
                path = PatchFieldValueFormatter.ResolveArrayFieldPath(operation.FieldPath, field, arrayField);
                oldValue = arrayField is null
                    ? "<missing>"
                    : PatchFieldValueFormatter.FormatArrayFieldValue(arrayField);
            }

            bool matches = field is not null && AssetFieldMatcher.MatchesFieldValue(field, operation.From);

            return
            [
                new PatchPreviewOperationResult(
                    path,
                    oldValue,
                    operation.From,
                    operation.To,
                    matches)
            ];
        }

        if (field is null)
        {
            return
            [
                new PatchPreviewOperationResult(
                    operation.FieldPath,
                    "<missing>",
                    operation.From,
                    operation.To,
                    false)
            ];
        }

        bool parentMatches = AssetFieldMatcher.MatchesFieldValue(field, operation.From);
        var results = new List<PatchPreviewOperationResult>();

        foreach (JsonProperty property in toObject.EnumerateObject())
        {
            AssetsFieldInfo? child = PatchFieldValueFormatter.FindDirectChild(field, property.Name);
            string childPath = $"{operation.FieldPath}.{property.Name}";
            bool isArrayPatch = PatchFieldValueFormatter.IsJsonArrayPatchValue(property.Value);
            string oldValue = isArrayPatch && child is not null
                ? PatchFieldValueFormatter.FormatArrayFieldValue(child)
                : child?.Value ?? "<missing>";

            results.Add(new PatchPreviewOperationResult(
                childPath,
                oldValue,
                PatchFieldValueFormatter.GetObjectPropertyOrDefault(operation.From, property.Name),
                property.Value.Clone(),
                parentMatches && child is not null && (child.Value is not null || isArrayPatch)));
        }

        return results;
    }

    private IReadOnlyList<PatchWriteOperation> CreatePatchWriteOperations(
        string assetsFilePath,
        long pathId,
        AssetsFieldInfo fieldTree,
        ManifestSetOperation operation)
    {
        operation = _valueResolver.ResolveSetOperation(assetsFilePath, operation);
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.FieldPath);

        if (!JsonUtils.TryGetObjectValue(operation.To, out JsonElement toObject))
        {
            if (PatchFieldValueFormatter.IsJsonArrayPatchValue(operation.To))
            {
                PatchFieldValueFormatter.EnsureSupportedPatchArrayValue(operation.To, operation.FieldPath);
                AssetsFieldInfo? arrayField = PatchFieldValueFormatter.ResolveArrayField(field);
                string path = PatchFieldValueFormatter.ResolveArrayFieldPath(operation.FieldPath, field, arrayField);
                string arrayOldValue = arrayField is null
                    ? "<missing>"
                    : PatchFieldValueFormatter.FormatArrayFieldValue(arrayField);

                if (field is null || arrayField is null || !AssetFieldMatcher.MatchesFieldValue(field, operation.From))
                {
                    throw new InvalidOperationException(
                        $"Patch operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': current value {arrayOldValue} does not match expected {JsonUtils.FormatElementValue(operation.From)}.");
                }

                return [new PatchWriteOperation(path, arrayOldValue, operation.To.Clone())];
            }

            PatchFieldValueFormatter.EnsureSupportedPatchValue(operation.To, operation.FieldPath);
            string oldValue = field?.Value ?? "<missing>";

            if (field is null || !AssetFieldMatcher.MatchesFieldValue(field, operation.From))
            {
                throw new InvalidOperationException(
                    $"Patch operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': current value {oldValue} does not match expected {JsonUtils.FormatElementValue(operation.From)}.");
            }

            return [new PatchWriteOperation(operation.FieldPath, oldValue, operation.To)];
        }

        string compositeOldValue = field is null ? "<missing>" : PatchFieldValueFormatter.FormatObjectFieldValue(field);

        if (field is null || !AssetFieldMatcher.MatchesFieldValue(field, operation.From))
        {
            throw new InvalidOperationException(
                $"Patch operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': current value {compositeOldValue} does not match expected {JsonUtils.FormatElementValue(operation.From)}.");
        }

        var operations = new List<PatchWriteOperation>();

        foreach (JsonProperty property in toObject.EnumerateObject())
        {
            string childPath = $"{operation.FieldPath}.{property.Name}";

            AssetsFieldInfo child = PatchFieldValueFormatter.FindDirectChild(field, property.Name)
                                    ?? throw new InvalidOperationException(
                                        $"Field not found for Path ID {pathId}: {childPath}");

            if (PatchFieldValueFormatter.IsJsonArrayPatchValue(property.Value))
            {
                PatchFieldValueFormatter.EnsureSupportedPatchArrayValue(property.Value, childPath);
                operations.Add(new PatchWriteOperation(
                    childPath,
                    PatchFieldValueFormatter.FormatArrayFieldValue(child),
                    property.Value.Clone()));
                continue;
            }

            PatchFieldValueFormatter.EnsureSupportedPatchValue(property.Value, childPath);

            string oldValue = child.Value ?? throw new InvalidOperationException(
                $"Patch operation cannot be applied for Path ID {pathId}, field '{childPath}': current value <missing> does not match expected {JsonUtils.FormatElementValue(PatchFieldValueFormatter.GetObjectPropertyOrDefault(operation.From, property.Name))}.");

            operations.Add(new PatchWriteOperation(childPath, oldValue, property.Value.Clone()));
        }

        return operations;
    }

    private static IReadOnlyList<PatchPreviewOperationResult> CreatePatchPreviewOperationResults(
        AssetsFieldInfo fieldTree,
        ManifestAddOperation operation)
    {
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.FieldPath);
        AssetsFieldInfo? arrayField = PatchFieldValueFormatter.ResolveArrayField(field);
        string path = PatchFieldValueFormatter.ResolveArrayFieldPath(operation.FieldPath, field, arrayField);

        if (arrayField is null)
        {
            return
            [
                new PatchPreviewOperationResult(
                    path,
                    "<missing>",
                    operation.Value,
                    operation.Value,
                    false)
            ];
        }

        PatchFieldValueFormatter.EnsureSupportedPatchArrayValue(operation.Value, operation.FieldPath);
        JsonElement to = PatchFieldValueFormatter.CreateAddArrayValue(arrayField, operation.Value, out bool willChange);

        return
        [
            new PatchPreviewOperationResult(
                path,
                PatchFieldValueFormatter.FormatArrayFieldValue(arrayField),
                operation.Value,
                to,
                willChange)
        ];
    }

    private static IReadOnlyList<PatchWriteOperation> CreatePatchWriteOperations(
        long pathId,
        AssetsFieldInfo fieldTree,
        ManifestAddOperation operation)
    {
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.FieldPath);
        AssetsFieldInfo? arrayField = PatchFieldValueFormatter.ResolveArrayField(field);
        string path = PatchFieldValueFormatter.ResolveArrayFieldPath(operation.FieldPath, field, arrayField);

        if (field is null || arrayField is null)
        {
            throw new InvalidOperationException(
                $"Patch add operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': field is not an array.");
        }

        PatchFieldValueFormatter.EnsureSupportedPatchArrayValue(operation.Value, operation.FieldPath);
        string oldValue = PatchFieldValueFormatter.FormatArrayFieldValue(arrayField);
        JsonElement to = PatchFieldValueFormatter.CreateAddArrayValue(arrayField, operation.Value, out bool willChange);

        return willChange ? [new PatchWriteOperation(path, oldValue, to)] : [];
    }
}
