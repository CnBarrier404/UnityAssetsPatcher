using System.Text.Json;
using Json.Schema;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

internal sealed record ManifestSchemaFailureContext(
    JsonElement Schema,
    JsonElement Instance,
    EvaluationResults Result,
    string Keyword,
    string InstancePath,
    string SchemaPath,
    string? PropertyName);

internal static class ManifestSchemaErrorMapper
{
    private const string PropertyNamesSchemaPath = "/propertyNames";
    private const string ComponentTypeSchemaPath = "/properties/componentType";
    private const string PatchConditionSchemaPath = "/$defs/patch/allOf";

    public static OperationError Map(JsonElement schema, JsonElement instance, EvaluationResults results)
    {
        ManifestSchemaFailure? failure = ManifestSchemaFailureSelector.Select(results);

        if (failure is null)
        {
            throw new InvalidOperationException("Schema validation failed without an error detail.");
        }

        ManifestSchemaFailureContext context = CreateContext(schema, instance, failure);

        return context.Keyword switch
        {
            "required" => MissingProperty(context),
            "type" => InvalidType(context),
            "const" when context.PropertyName == "$schema" => UnsupportedSchema(context),
            _ when IsFieldPathConstraint(context) => InvalidFieldPath(context),
            _ when IsComponentTypeConstraint(context) => InvalidComponentType(context),
            "pattern" when IsPatchConditionConstraint(context) => InvalidComponentType(context),
            "not" when IsPatchConditionConstraint(context) => ConflictingPatchProperties(context),
            "minLength" or "pattern" => Failure(ManifestErrorCodes.BlankProperty, context),
            "minItems" => Failure(ManifestErrorCodes.EmptyCollection, context),
            "minProperties" => Failure(ManifestErrorCodes.EmptyObject, context),
            _ => throw new InvalidOperationException(
                $"Unsupported manifest schema failure '{context.Keyword}' at '{context.SchemaPath}'."),
        };
    }

    private static ManifestSchemaFailureContext CreateContext(
        JsonElement schema,
        JsonElement instance,
        ManifestSchemaFailure failure)
    {
        EvaluationResults result = failure.Result;
        string instancePath = result.InstanceLocation.ToString();

        return new ManifestSchemaFailureContext(
            schema,
            instance,
            result,
            failure.Error.Key,
            instancePath,
            result.SchemaLocation.Fragment,
            ManifestJsonPointer.PropertyName(instancePath));
    }

    private static OperationError MissingProperty(ManifestSchemaFailureContext context)
    {
        string schemaPath = Uri.UnescapeDataString(context.Result.SchemaLocation.Fragment.TrimStart('#'));
        var schemaOwner = ManifestJsonPointer.Resolve(context.Schema, schemaPath);
        var instanceOwner = ManifestJsonPointer.Resolve(context.Instance, context.InstancePath);
        string? missingProperty = FindMissingProperty(schemaOwner, instanceOwner);
        var parameters = CreateParameters(context);
        parameters["property"] = missingProperty;

        return new OperationError(ManifestErrorCodes.MissingProperty, parameters);
    }

    private static string? FindMissingProperty(JsonElement? schemaOwner, JsonElement? instanceOwner)
    {
        if (schemaOwner is not { ValueKind: JsonValueKind.Object } ||
            instanceOwner is not { ValueKind: JsonValueKind.Object } ||
            !schemaOwner.Value.TryGetProperty("required", out JsonElement required))
        {
            return null;
        }

        return required.EnumerateArray().Select(property => property.GetString()).OfType<string>()
            .FirstOrDefault(propertyName => !instanceOwner.Value.TryGetProperty(propertyName, out _));
    }

    private static OperationError InvalidType(ManifestSchemaFailureContext context)
    {
        string schemaPath = Uri.UnescapeDataString(context.Result.SchemaLocation.Fragment.TrimStart('#'));
        var schemaNode = ManifestJsonPointer.ResolveSchemaConstraint(context.Schema, schemaPath, "type");
        var parameters = CreateParameters(context);
        parameters["property"] = context.PropertyName;
        parameters["expected"] = schemaNode is { ValueKind: JsonValueKind.Object } &&
                                 schemaNode.Value.TryGetProperty("type", out JsonElement type)
            ? type.GetString()
            : null;

        return new OperationError(ManifestErrorCodes.InvalidPropertyType, parameters);
    }

    private static OperationError UnsupportedSchema(ManifestSchemaFailureContext context)
    {
        var actual = ManifestJsonPointer.Resolve(context.Instance, context.InstancePath);
        var parameters = CreateParameters(context);
        parameters["actual"] = actual?.ValueKind == JsonValueKind.String ? actual.Value.GetString() : null;
        parameters["supported"] = ManifestSchemaValidator.CurrentSchema;

        return new OperationError(ManifestErrorCodes.UnsupportedSchema, parameters);
    }

    private static OperationError InvalidFieldPath(ManifestSchemaFailureContext context)
    {
        int separatorIndex = context.InstancePath.LastIndexOf('/');
        var parameters = CreateParameters(context);
        parameters["path"] = separatorIndex < 0
            ? string.Empty
            : ManifestJsonPointer.DecodeSegment(context.InstancePath[(separatorIndex + 1)..]);
        parameters["expected"] = "non_empty_field_path";

        return new OperationError(ManifestErrorCodes.InvalidPath, parameters);
    }

    private static OperationError InvalidComponentType(ManifestSchemaFailureContext context)
    {
        var parameters = CreateParameters(context);
        parameters["property"] = "componentType";
        parameters["required_asset_type"] = "GameObject";

        return new OperationError(ManifestErrorCodes.InvalidComponentType, parameters);
    }

    private static OperationError ConflictingPatchProperties(ManifestSchemaFailureContext context)
    {
        var parameters = CreateParameters(context);
        parameters["property"] = "componentType";
        parameters["conflicts_with"] = "replaceAsset";

        return new OperationError(ManifestErrorCodes.ConflictingProperties, parameters);
    }

    private static OperationError Failure(OperationErrorCode code, ManifestSchemaFailureContext context)
    {
        var parameters = CreateParameters(context);
        parameters["property"] = context.PropertyName;

        if (context.PropertyName == "match")
        {
            parameters["owner"] = "patch.match";
        }

        return new OperationError(code, parameters);
    }

    private static Dictionary<string, object?> CreateParameters(ManifestSchemaFailureContext context)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["instance_path"] = context.InstancePath,
            ["schema_path"] = context.SchemaPath,
            ["keyword"] = context.Keyword,
        };
    }

    private static bool IsFieldPathConstraint(ManifestSchemaFailureContext context)
    {
        return context.SchemaPath.Contains(PropertyNamesSchemaPath, StringComparison.Ordinal);
    }

    private static bool IsComponentTypeConstraint(ManifestSchemaFailureContext context)
    {
        return context.SchemaPath.Contains(ComponentTypeSchemaPath, StringComparison.Ordinal);
    }

    private static bool IsPatchConditionConstraint(ManifestSchemaFailureContext context)
    {
        return context.SchemaPath.Contains(PatchConditionSchemaPath, StringComparison.Ordinal);
    }
}
