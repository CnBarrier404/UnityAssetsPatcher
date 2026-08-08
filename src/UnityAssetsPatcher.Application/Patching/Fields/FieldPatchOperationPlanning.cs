using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Json;

namespace UnityAssetsPatcher.Application.Patching.Fields;

public sealed class FieldPatchOperationPlanner
{
    public static IReadOnlyList<FieldPatchOperationPlan> CreateSetOperationPlans(
        long pathId,
        AssetField fieldTree,
        ModSetOperation operation)
    {
        AssetField? field = AssetFieldNavigator.Find(fieldTree, operation.FieldPath);

        return JsonUtils.TryGetObjectValue(operation.To, out JsonElement toObject)
            ? CreateObjectSetOperationPlans(pathId, field, operation, toObject)
            : [CreateScalarSetOperationPlan(pathId, field, operation)];
    }

    public static IReadOnlyList<FieldPatchOperationPlan> CreateAddOperationPlans(
        long pathId,
        AssetField fieldTree,
        ModAddOperation operation)
    {
        AssetField? field = AssetFieldNavigator.Find(fieldTree, operation.FieldPath);
        AssetField? arrayField = PatchFieldValueConverter.ResolveArrayField(field);
        string path = PatchFieldValueConverter.ResolveArrayFieldPath(operation.FieldPath, field, arrayField);

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
                    CreateFailure(
                        PatchDiagnosticCode.InvalidPatchConfiguration,
                        $"Patch add operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': field is not an array."),
                    PatchWriteValueValidation.None,
                    false)
            ];
        }

        PatchFieldValueConverter.EnsureSupportedPatchArrayValue(operation.Value, operation.FieldPath);
        JsonElement to = PatchFieldValueConverter.CreateAddArrayValue(arrayField, operation.Value, out bool willChange);

        return
        [
            new FieldPatchOperationPlan(
                path,
                PatchFieldValueConverter.FormatArrayFieldValue(arrayField),
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
        AssetField? field,
        ModSetOperation operation)
    {
        FieldValueSnapshot value = FieldValueSnapshot.ForSetOperation(field, operation);
        bool matches = field is not null && AssetFieldMatcher.MatchesValue(field, operation.From);
        PatchDiagnostic? failure = field is null || !matches || value is { IsArrayPatch: true, ArrayField: null }
            ? CreateSetMismatchFailure(pathId, operation.FieldPath, value.OldValue, operation.From)
            : null;

        return new FieldPatchOperationPlan(
            value.Path,
            value.OldValue,
            operation.From,
            operation.To,
            matches,
            true,
            failure,
            value.IsArrayPatch ? PatchWriteValueValidation.Array : PatchWriteValueValidation.Scalar,
            true,
            operation.FieldPath);
    }

    private static IReadOnlyList<FieldPatchOperationPlan> CreateObjectSetOperationPlans(
        long pathId,
        AssetField? field,
        ModSetOperation operation,
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
                    CreateSetMismatchFailure(pathId, operation.FieldPath, "<missing>", operation.From),
                    PatchWriteValueValidation.None,
                    false)
            ];
        }

        bool parentMatches = AssetFieldMatcher.MatchesValue(field, operation.From);
        PatchDiagnostic? parentFailure = parentMatches
            ? null
            : CreateSetMismatchFailure(
                pathId,
                operation.FieldPath,
                PatchFieldValueConverter.FormatObjectFieldValue(field),
                operation.From);

        return toObject
            .EnumerateObject()
            .Select(property => CreateObjectChildSetOperationPlan(
                pathId,
                field,
                operation,
                property,
                parentMatches,
                parentFailure))
            .ToArray();
    }

    private static FieldPatchOperationPlan CreateObjectChildSetOperationPlan(
        long pathId,
        AssetField parentField,
        ModSetOperation operation,
        JsonProperty property,
        bool parentMatches,
        PatchDiagnostic? parentFailure)
    {
        AssetField? child = PatchFieldValueConverter.Child(parentField, property.Name);
        string childPath = $"{operation.FieldPath}.{property.Name}";
        bool isArrayPatch = PatchFieldValueConverter.IsJsonArrayPatchValue(property.Value);
        JsonElement from = PatchFieldValueConverter.GetObjectPropertyOrDefault(operation.From, property.Name);
        string oldValue = CreateChildOldValue(child, isArrayPatch);
        ChildWritePolicy writePolicy = CreateChildWritePolicy(
            pathId,
            childPath,
            child,
            from,
            isArrayPatch,
            parentFailure);

        return new FieldPatchOperationPlan(
            childPath,
            oldValue,
            from,
            property.Value.Clone(),
            parentMatches && child is not null && (child.Value is not null || isArrayPatch),
            true,
            writePolicy.Failure,
            writePolicy.ValueValidation,
            writePolicy.ValidateBeforeFailure);
    }

    private static ChildWritePolicy CreateChildWritePolicy(
        long pathId,
        string childPath,
        AssetField? child,
        JsonElement from,
        bool isArrayPatch,
        PatchDiagnostic? parentFailure)
    {
        if (parentFailure is not null)
        {
            return ChildWritePolicy.Failing(parentFailure);
        }

        if (child is null)
        {
            return ChildWritePolicy.Failing(CreateFailure(
                PatchDiagnosticCode.FieldNotFound,
                $"Field not found for Path ID {pathId}: {childPath}"));
        }

        if (isArrayPatch)
        {
            return ChildWritePolicy.ValidArray();
        }

        return child.Value is null
            ? ChildWritePolicy.FailingScalar(CreateSetMismatchFailure(pathId, childPath, "<missing>", from))
            : ChildWritePolicy.ValidScalar();
    }

    private static string CreateChildOldValue(AssetField? child, bool isArrayPatch)
    {
        return isArrayPatch && child is not null
            ? PatchFieldValueConverter.FormatArrayFieldValue(child)
            : child?.Value?.ToInvariantString() ?? "<missing>";
    }

    private static PatchDiagnostic CreateSetMismatchFailure(
        long pathId,
        string fieldPath,
        string oldValue,
        JsonElement expectedValue)
    {
        return CreateFailure(
            PatchDiagnosticCode.ValueMismatch,
            $"Patch operation cannot be applied for Path ID {pathId}, field '{fieldPath}': current value {oldValue} does not match expected {JsonUtils.FormatElementValue(expectedValue)}.");
    }

    private static PatchDiagnostic CreateFailure(PatchDiagnosticCode code, string detail)
    {
        return new PatchDiagnostic(code, "", Detail: detail);
    }

    private sealed record FieldValueSnapshot(
        string Path,
        string OldValue,
        bool IsArrayPatch,
        AssetField? ArrayField)
    {
        public static FieldValueSnapshot ForSetOperation(AssetField? field, ModSetOperation operation)
        {
            if (!PatchFieldValueConverter.IsJsonArrayPatchValue(operation.To))
            {
                return new FieldValueSnapshot(
                    operation.FieldPath, field?.Value?.ToInvariantString() ?? "<missing>", false, null);
            }

            AssetField? arrayField = PatchFieldValueConverter.ResolveArrayField(field);
            string path = PatchFieldValueConverter.ResolveArrayFieldPath(operation.FieldPath, field, arrayField);
            string oldValue = arrayField is null
                ? "<missing>"
                : PatchFieldValueConverter.FormatArrayFieldValue(arrayField);

            return new FieldValueSnapshot(path, oldValue, true, arrayField);
        }
    }

    private sealed record ChildWritePolicy(
        PatchDiagnostic? Failure,
        PatchWriteValueValidation ValueValidation,
        bool ValidateBeforeFailure)
    {
        public static ChildWritePolicy Failing(PatchDiagnostic failure)
        {
            return new ChildWritePolicy(failure, PatchWriteValueValidation.None, false);
        }

        public static ChildWritePolicy FailingScalar(PatchDiagnostic failure)
        {
            return new ChildWritePolicy(failure, PatchWriteValueValidation.Scalar, true);
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

public sealed record FieldPatchOperationPlan(
    string Path,
    string OldValue,
    JsonElement From,
    JsonElement To,
    bool WillChange,
    bool WriteRequired,
    PatchDiagnostic? WriteFailure,
    PatchWriteValueValidation WriteValueValidation,
    bool ValidateBeforeFailure,
    string? WriteValueValidationPath = null);

public enum PatchWriteValueValidation
{
    None,
    Scalar,
    Array,
}

public static class FieldPatchWriteOperationMapper
{
    public static void AddTo(
        ICollection<FieldPatchOperation> operations,
        FieldPatchOperationPlan operation)
    {
        if (operation.ValidateBeforeFailure)
        {
            ValidateWriteValue(operation);
        }

        if (operation.WriteFailure is not null)
        {
            throw new PatchPlanningException(operation.WriteFailure);
        }

        if (!operation.ValidateBeforeFailure)
        {
            ValidateWriteValue(operation);
        }

        if (operation.WriteRequired)
        {
            operations.Add(new FieldPatchOperation(operation.Path, operation.To.Clone()));
        }
    }

    private static void ValidateWriteValue(FieldPatchOperationPlan operation)
    {
        switch (operation.WriteValueValidation)
        {
            case PatchWriteValueValidation.None:
                break;
            case PatchWriteValueValidation.Scalar:
                PatchFieldValueConverter.EnsureSupportedPatchValue(
                    operation.To,
                    operation.WriteValueValidationPath ?? operation.Path);
                break;
            case PatchWriteValueValidation.Array:
                PatchFieldValueConverter.EnsureSupportedPatchArrayValue(
                    operation.To,
                    operation.WriteValueValidationPath ?? operation.Path);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
