using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Patching;

internal sealed class FieldPatchOperationPlanner
{
    private readonly PatchValueResolver _valueResolver;

    public FieldPatchOperationPlanner(PatchValueResolver valueResolver)
    {
        _valueResolver = valueResolver;
    }

    public IReadOnlyList<FieldPatchOperationPlan> CreateSetOperationPlans(
        string assetsFilePath,
        long pathId,
        AssetsFieldInfo fieldTree,
        ManifestSetOperation operation)
    {
        operation = _valueResolver.ResolveSetOperation(assetsFilePath, operation);
        AssetsFieldInfo? field = AssetFieldNavigator.FindField(fieldTree, operation.FieldPath);

        return JsonUtils.TryGetObjectValue(operation.To, out JsonElement toObject)
            ? CreateObjectSetOperationPlans(pathId, field, operation, toObject)
            : [CreateScalarSetOperationPlan(pathId, field, operation)];
    }

    public static IReadOnlyList<FieldPatchOperationPlan> CreateAddOperationPlans(
        long pathId,
        AssetsFieldInfo fieldTree,
        ManifestAddOperation operation)
    {
        AssetsFieldInfo? field = AssetFieldNavigator.FindField(fieldTree, operation.FieldPath);
        AssetsFieldInfo? arrayField = PatchFieldValueFormatter.ResolveArrayField(field);
        string path = PatchFieldValueFormatter.ResolveArrayFieldPath(operation.FieldPath, field, arrayField);

        if (arrayField is null)
        {
            return
            [
                new FieldPatchOperationPlan(
                    path,
                    "<missing>",
                    operation.Value,
                    operation.Value,
                    false,
                    true,
                    $"Patch add operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': field is not an array.",
                    PatchWriteValueValidation.None,
                    false)
            ];
        }

        PatchFieldValueFormatter.EnsureSupportedPatchArrayValue(operation.Value, operation.FieldPath);
        JsonElement to = PatchFieldValueFormatter.CreateAddArrayValue(arrayField, operation.Value, out bool willChange);

        return
        [
            new FieldPatchOperationPlan(
                path,
                PatchFieldValueFormatter.FormatArrayFieldValue(arrayField),
                operation.Value,
                to,
                willChange,
                willChange,
                null,
                PatchWriteValueValidation.None,
                false)
        ];
    }

    private static FieldPatchOperationPlan CreateScalarSetOperationPlan(
        long pathId,
        AssetsFieldInfo? field,
        ManifestSetOperation operation)
    {
        FieldValueSnapshot value = FieldValueSnapshot.ForSetOperation(field, operation);
        bool matches = field is not null && AssetFieldMatcher.MatchesFieldValue(field, operation.From);
        string? failureMessage = field is null || !matches || value is { IsArrayPatch: true, ArrayField: null }
            ? CreateSetMismatchMessage(pathId, operation.FieldPath, value.OldValue, operation.From)
            : null;

        return new FieldPatchOperationPlan(
            value.Path,
            value.OldValue,
            operation.From,
            operation.To,
            matches,
            true,
            failureMessage,
            value.IsArrayPatch ? PatchWriteValueValidation.Array : PatchWriteValueValidation.Scalar,
            true,
            operation.FieldPath);
    }

    private static IReadOnlyList<FieldPatchOperationPlan> CreateObjectSetOperationPlans(
        long pathId,
        AssetsFieldInfo? field,
        ManifestSetOperation operation,
        JsonElement toObject)
    {
        if (field is null)
        {
            return
            [
                new FieldPatchOperationPlan(
                    operation.FieldPath,
                    "<missing>",
                    operation.From,
                    operation.To,
                    false,
                    true,
                    CreateSetMismatchMessage(pathId, operation.FieldPath, "<missing>", operation.From),
                    PatchWriteValueValidation.None,
                    false)
            ];
        }

        bool parentMatches = AssetFieldMatcher.MatchesFieldValue(field, operation.From);
        string? parentFailureMessage = parentMatches
            ? null
            : CreateSetMismatchMessage(
                pathId,
                operation.FieldPath,
                PatchFieldValueFormatter.FormatObjectFieldValue(field),
                operation.From);

        return toObject
            .EnumerateObject()
            .Select(property => CreateObjectChildSetOperationPlan(
                pathId,
                field,
                operation,
                property,
                parentMatches,
                parentFailureMessage))
            .ToArray();
    }

    private static FieldPatchOperationPlan CreateObjectChildSetOperationPlan(
        long pathId,
        AssetsFieldInfo parentField,
        ManifestSetOperation operation,
        JsonProperty property,
        bool parentMatches,
        string? parentFailureMessage)
    {
        AssetsFieldInfo? child = PatchFieldValueFormatter.FindDirectChild(parentField, property.Name);
        string childPath = $"{operation.FieldPath}.{property.Name}";
        bool isArrayPatch = PatchFieldValueFormatter.IsJsonArrayPatchValue(property.Value);
        JsonElement from = PatchFieldValueFormatter.GetObjectPropertyOrDefault(operation.From, property.Name);
        string oldValue = CreateChildOldValue(child, isArrayPatch);
        ChildWritePolicy writePolicy = CreateChildWritePolicy(
            pathId,
            childPath,
            child,
            from,
            isArrayPatch,
            parentFailureMessage);

        return new FieldPatchOperationPlan(
            childPath,
            oldValue,
            from,
            property.Value.Clone(),
            parentMatches && child is not null && (child.Value is not null || isArrayPatch),
            true,
            writePolicy.FailureMessage,
            writePolicy.ValueValidation,
            writePolicy.ValidateBeforeFailure);
    }

    private static ChildWritePolicy CreateChildWritePolicy(
        long pathId,
        string childPath,
        AssetsFieldInfo? child,
        JsonElement from,
        bool isArrayPatch,
        string? parentFailureMessage)
    {
        if (parentFailureMessage is not null)
        {
            return ChildWritePolicy.Failing(parentFailureMessage);
        }

        if (child is null)
        {
            return ChildWritePolicy.Failing($"Field not found for Path ID {pathId}: {childPath}");
        }

        if (isArrayPatch)
        {
            return ChildWritePolicy.ValidArray();
        }

        return child.Value is null
            ? ChildWritePolicy.FailingScalar(CreateSetMismatchMessage(pathId, childPath, "<missing>", from))
            : ChildWritePolicy.ValidScalar();
    }

    private static string CreateChildOldValue(AssetsFieldInfo? child, bool isArrayPatch)
    {
        return isArrayPatch && child is not null
            ? PatchFieldValueFormatter.FormatArrayFieldValue(child)
            : child?.Value ?? "<missing>";
    }

    private static string CreateSetMismatchMessage(
        long pathId,
        string fieldPath,
        string oldValue,
        JsonElement expectedValue)
    {
        return
            $"Patch operation cannot be applied for Path ID {pathId}, field '{fieldPath}': current value {oldValue} does not match expected {JsonUtils.FormatElementValue(expectedValue)}.";
    }

    private sealed record FieldValueSnapshot(
        string Path,
        string OldValue,
        bool IsArrayPatch,
        AssetsFieldInfo? ArrayField)
    {
        public static FieldValueSnapshot ForSetOperation(AssetsFieldInfo? field, ManifestSetOperation operation)
        {
            if (!PatchFieldValueFormatter.IsJsonArrayPatchValue(operation.To))
            {
                return new FieldValueSnapshot(operation.FieldPath, field?.Value ?? "<missing>", false, null);
            }

            AssetsFieldInfo? arrayField = PatchFieldValueFormatter.ResolveArrayField(field);
            string path = PatchFieldValueFormatter.ResolveArrayFieldPath(operation.FieldPath, field, arrayField);
            string oldValue = arrayField is null
                ? "<missing>"
                : PatchFieldValueFormatter.FormatArrayFieldValue(arrayField);

            return new FieldValueSnapshot(path, oldValue, true, arrayField);
        }
    }

    private sealed record ChildWritePolicy(
        string? FailureMessage,
        PatchWriteValueValidation ValueValidation,
        bool ValidateBeforeFailure)
    {
        public static ChildWritePolicy Failing(string failureMessage)
        {
            return new ChildWritePolicy(failureMessage, PatchWriteValueValidation.None, false);
        }

        public static ChildWritePolicy FailingScalar(string failureMessage)
        {
            return new ChildWritePolicy(failureMessage, PatchWriteValueValidation.Scalar, true);
        }

        public static ChildWritePolicy ValidScalar()
        {
            return new ChildWritePolicy(null, PatchWriteValueValidation.Scalar, true);
        }

        public static ChildWritePolicy ValidArray()
        {
            return new ChildWritePolicy(null, PatchWriteValueValidation.Array, false);
        }
    }
}
