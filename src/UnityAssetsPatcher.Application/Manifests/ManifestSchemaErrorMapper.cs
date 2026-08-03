using System.Text.Json;
using Json.Schema;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Manifests;

internal static class ManifestSchemaErrorMapper
{
    public static OperationError Map(JsonElement schema, JsonElement instance, EvaluationResults results)
    {
        ManifestSchemaFailure? failure = ManifestSchemaFailureSelector.Select(results);

        if (failure is null)
        {
            return InvalidValue(string.Empty, string.Empty, "schema");
        }

        EvaluationResults failureResult = failure.Result;
        string keyword = failure.Error.Key;
        string instancePath = failureResult.InstanceLocation.ToString();
        string schemaPath = failureResult.SchemaLocation.Fragment;
        string? propertyName = ManifestJsonPointer.PropertyName(instancePath);
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["instance_path"] = instancePath,
            ["schema_path"] = schemaPath,
            ["keyword"] = keyword,
        };

        return keyword switch
        {
            "required" => MissingProperty(schema, instance, failureResult, parameters),
            "type" => InvalidType(schema, failureResult, propertyName, parameters),
            "const" when propertyName == "$schema" => UnsupportedSchema(instance, instancePath, parameters),
            _ when schemaPath.Contains("/propertyNames", StringComparison.Ordinal) => InvalidFieldPath(parameters),
            "pattern" when schemaPath.Contains("/$defs/patch/allOf", StringComparison.Ordinal) =>
                InvalidComponentType(parameters),
            "not" when schemaPath.Contains("/$defs/patch/allOf", StringComparison.Ordinal) =>
                ConflictingPatchProperties(parameters),
            _ => InvalidValue(propertyName, parameters),
        };
    }

    private static OperationError MissingProperty(
        JsonElement schema,
        JsonElement instance,
        EvaluationResults failure,
        Dictionary<string, object?> parameters)
    {
        string schemaPath = Uri.UnescapeDataString(failure.SchemaLocation.Fragment.TrimStart('#'));
        string instancePath = failure.InstanceLocation.ToString();
        var schemaOwner = ManifestJsonPointer.Resolve(schema, schemaPath);
        var instanceOwner = ManifestJsonPointer.Resolve(instance, instancePath);
        string? missingProperty = FindMissingProperty(schemaOwner, instanceOwner);

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

    private static OperationError InvalidType(
        JsonElement schema,
        EvaluationResults failure,
        string? propertyName,
        Dictionary<string, object?> parameters)
    {
        string schemaPath = Uri.UnescapeDataString(failure.SchemaLocation.Fragment.TrimStart('#'));
        var schemaNode = ManifestJsonPointer.ResolveSchemaConstraint(schema, schemaPath, "type");

        parameters["property"] = propertyName;
        parameters["expected"] = schemaNode is { ValueKind: JsonValueKind.Object } &&
                                 schemaNode.Value.TryGetProperty("type", out JsonElement type)
            ? type.GetString()
            : null;

        return new OperationError(ManifestErrorCodes.InvalidPropertyType, parameters);
    }

    private static OperationError UnsupportedSchema(
        JsonElement instance,
        string instancePath,
        Dictionary<string, object?> parameters)
    {
        var actual = ManifestJsonPointer.Resolve(instance, instancePath);

        parameters["actual"] = actual?.ValueKind == JsonValueKind.String ? actual.Value.GetString() : null;
        parameters["supported"] = ManifestSchemaValidator.CurrentSchema;

        return new OperationError(ManifestErrorCodes.UnsupportedSchema, parameters);
    }

    private static OperationError InvalidFieldPath(Dictionary<string, object?> parameters)
    {
        string instancePath = parameters["instance_path"] as string ?? string.Empty;
        int separatorIndex = instancePath.LastIndexOf('/');

        parameters["path"] = separatorIndex < 0
            ? string.Empty
            : ManifestJsonPointer.DecodeSegment(instancePath[(separatorIndex + 1)..]);
        parameters["expected"] = "non_empty_field_path";

        return new OperationError(ManifestErrorCodes.InvalidPath, parameters);
    }

    private static OperationError InvalidComponentType(Dictionary<string, object?> parameters)
    {
        parameters["property"] = "componentType";
        parameters["required_asset_type"] = "GameObject";

        return new OperationError(ManifestErrorCodes.InvalidValue, parameters);
    }

    private static OperationError ConflictingPatchProperties(Dictionary<string, object?> parameters)
    {
        parameters["property"] = "componentType";
        parameters["conflicts_with"] = "replaceAsset";

        return new OperationError(ManifestErrorCodes.InvalidValue, parameters);
    }

    private static OperationError InvalidValue(string? propertyName, Dictionary<string, object?> parameters)
    {
        parameters["property"] = propertyName;

        if (propertyName == "match")
        {
            parameters["owner"] = "Manifest patch match";
        }

        return new OperationError(ManifestErrorCodes.InvalidValue, parameters);
    }

    private static OperationError InvalidValue(string instancePath, string schemaPath, string keyword)
    {
        return new OperationError(
            ManifestErrorCodes.InvalidValue,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["instance_path"] = instancePath,
                ["schema_path"] = schemaPath,
                ["keyword"] = keyword,
            });
    }
}
