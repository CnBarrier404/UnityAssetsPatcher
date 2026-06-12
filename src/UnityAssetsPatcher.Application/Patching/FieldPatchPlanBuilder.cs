using System.Globalization;
using System.Text;
using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class FieldPatchPlanBuilder
{
    private readonly AssetQueryService _assetQueryService;
    private readonly FieldPatchOperationPlanner _operationPlanner;

    public FieldPatchPlanBuilder(AssetQueryService assetQueryService)
    {
        _assetQueryService = assetQueryService;
        _operationPlanner = new FieldPatchOperationPlanner(new PatchValueResolver(assetQueryService));
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
            foreach (AssetQueryMatch match in AssetQueryService.FindMatches(queryContext, patch))
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

internal sealed class PatchValueResolver
{
    private readonly AssetQueryService _assetQueryService;

    public PatchValueResolver(AssetQueryService assetQueryService)
    {
        _assetQueryService = assetQueryService;
    }

    public ManifestSetOperation ResolveSetOperation(string assetsFilePath, ManifestSetOperation operation)
    {
        JsonElement resolvedTo = ResolvePatchValue(assetsFilePath, operation.To);

        return new ManifestSetOperation(operation.FieldPath, operation.From.Clone(), resolvedTo);
    }

    private JsonElement ResolvePatchValue(string assetsFilePath, JsonElement value)
    {
        if (!TryGetPathIdResolver(value, out JsonElement resolver))
        {
            return value.Clone();
        }

        long pathId = ResolvePathIdReference(assetsFilePath, resolver);
        return JsonElementFactory.Number(pathId);
    }

    private long ResolvePathIdReference(string assetsFilePath, JsonElement resolver)
    {
        string type = ReadRequiredPathIdResolverString(resolver, "type");
        var includeGroups = ReadPathIdResolverMatchGroups(resolver);

        var target = new ManifestPatch(
            Path.GetFileName(assetsFilePath),
            type,
            includeGroups,
            null,
            null);
        var matches = _assetQueryService.FindMatches(assetsFilePath, target)
            .Select(match => match.Asset)
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0].PathId,
            0 => throw new InvalidOperationException(
                $"Path ID reference did not match any assets for type '{type}'."),
            _ => throw new InvalidOperationException(
                $"Path ID reference matched multiple assets for type '{type}'.")
        };
    }

    private static bool TryGetPathIdResolver(JsonElement value, out JsonElement resolver)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.EnumerateObject().Count() == 1 &&
            value.TryGetProperty("$pathId", out resolver) &&
            resolver.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        resolver = default;
        return false;
    }

    private static string ReadRequiredPathIdResolverString(JsonElement resolver, string propertyName)
    {
        if (!resolver.TryGetProperty(propertyName, out JsonElement propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"Path ID reference must contain a non-empty string '{propertyName}' property.");
        }

        string? value = propertyElement.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Path ID reference must contain a non-empty string '{propertyName}' property.")
            : value;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadPathIdResolverMatchGroups(
        JsonElement resolver)
    {
        if (!resolver.TryGetProperty("match", out JsonElement matchElement) ||
            matchElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Path ID reference must contain a 'match' object.");
        }

        var includeGroup = matchElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);

        return includeGroup.Count == 0
            ? throw new InvalidOperationException("Path ID reference match object cannot be empty.")
            : [includeGroup];
    }
}

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

internal sealed record FieldPatchOperationPlan(
    string Path,
    string OldValue,
    JsonElement From,
    JsonElement To,
    bool WillChange,
    bool WriteRequired,
    string? WriteFailureMessage,
    PatchWriteValueValidation WriteValueValidation,
    bool ValidateBeforeFailure,
    string? WriteValueValidationPath = null);

internal enum PatchWriteValueValidation
{
    None,
    Scalar,
    Array,
}

internal static class FieldPatchWriteOperationMapper
{
    public static void AddTo(
        ICollection<FieldPatchOperation> operations,
        FieldPatchOperationPlan operation)
    {
        if (operation.ValidateBeforeFailure)
        {
            ValidateWriteValue(operation);
        }

        if (operation.WriteFailureMessage is not null)
        {
            throw new InvalidOperationException(operation.WriteFailureMessage);
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
                PatchFieldValueFormatter.EnsureSupportedPatchValue(
                    operation.To,
                    operation.WriteValueValidationPath ?? operation.Path);
                break;
            case PatchWriteValueValidation.Array:
                PatchFieldValueFormatter.EnsureSupportedPatchArrayValue(
                    operation.To,
                    operation.WriteValueValidationPath ?? operation.Path);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

internal static class PatchFieldValueFormatter
{
    public static AssetsFieldInfo? FindDirectChild(AssetsFieldInfo field, string name)
    {
        return AssetFieldNavigator.FindDirectChild(field, name);
    }

    public static bool IsJsonArrayPatchValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Array &&
               !JsonUtils.TryGetObjectValue(value, out _);
    }

    public static AssetsFieldInfo? ResolveArrayField(AssetsFieldInfo? field)
    {
        return AssetFieldNavigator.ResolveArrayField(field);
    }

    public static string ResolveArrayFieldPath(
        string fieldPath,
        AssetsFieldInfo? field,
        AssetsFieldInfo? arrayField)
    {
        return field is not null && arrayField is not null && !ReferenceEquals(field, arrayField)
            ? $"{fieldPath}.{arrayField.Name}"
            : fieldPath;
    }

    public static IReadOnlyList<AssetsFieldInfo> GetArrayElementFields(AssetsFieldInfo arrayField)
    {
        return AssetFieldNavigator.GetArrayElementFields(arrayField);
    }

    public static JsonElement GetObjectPropertyOrDefault(JsonElement value, string propertyName)
    {
        return JsonUtils.TryGetObjectValue(value, out JsonElement objectValue) &&
               objectValue.TryGetProperty(propertyName, out JsonElement propertyValue)
            ? propertyValue.Clone()
            : value;
    }

    public static string FormatObjectFieldValue(AssetsFieldInfo field)
    {
        string properties = string.Join(", ", field.Children
            .Where(child => child.Value is not null)
            .Select(child => $"{child.Name}: {child.Value}"));

        return properties.Length == 0 ? "<missing>" : $"{{ {properties} }}";
    }

    public static string FormatArrayFieldValue(AssetsFieldInfo arrayField)
    {
        string elements = string.Join(", ", GetArrayElementFields(arrayField).Select(FormatArrayElementValue));

        return $"[{elements}]";
    }

    public static JsonElement CreateAddArrayValue(
        AssetsFieldInfo arrayField,
        JsonElement value,
        out bool changed)
    {
        var currentFields = GetArrayElementFields(arrayField);
        var elements = currentFields
            .Select(CreateJsonElementFromArrayElementField)
            .ToList();
        changed = false;

        foreach (JsonElement element in value.EnumerateArray()
                     .Where(element => !ContainsArrayValue(currentFields, elements, element)))
        {
            elements.Add(element.Clone());
            changed = true;
        }

        return JsonElementFactory.Array(elements);
    }

    public static void EnsureSupportedPatchValue(JsonElement value, string path)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number
            or JsonValueKind.String)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Patch operation for field '{path}' uses an unsupported value type: {value.ValueKind}.");
    }

    public static void EnsureSupportedPatchArrayValue(JsonElement value, string path)
    {
        int index = 0;

        foreach (JsonElement element in value.EnumerateArray())
        {
            EnsureSupportedPatchValue(element, $"{path}[{index}]");
            index++;
        }
    }

    private static string FormatArrayElementValue(AssetsFieldInfo element)
    {
        if (element.Value is null)
        {
            return FormatObjectFieldValue(element);
        }

        return string.Equals(element.TypeName, "string", StringComparison.OrdinalIgnoreCase)
            ? FormatJsonStringLiteral(element.Value)
            : element.Value;
    }

    private static string FormatJsonStringLiteral(string value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStringValue(value);
        }

        return stream.TryGetBuffer(out var buffer)
            ? Encoding.UTF8.GetString(buffer.AsSpan())
            : Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool ContainsArrayValue(
        IReadOnlyList<AssetsFieldInfo> currentFields,
        IReadOnlyList<JsonElement> elements,
        JsonElement value)
    {
        if (currentFields.Any(field => AssetFieldMatcher.MatchesFieldValue(field, value)))
        {
            return true;
        }

        return elements
            .Skip(currentFields.Count)
            .Any(element => JsonScalarValuesEqual(element, value));
    }

    private static bool JsonScalarValuesEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            right.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return left.GetBoolean() == right.GetBoolean();
        }

        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            return left.TryGetDouble(out double leftNumber) &&
                   right.TryGetDouble(out double rightNumber) &&
                   Math.Abs(leftNumber - rightNumber) <= 0.00001d;
        }

        return left.ValueKind == right.ValueKind &&
               string.Equals(JsonUtils.FormatElementValue(left), JsonUtils.FormatElementValue(right),
                   StringComparison.Ordinal);
    }

    private static JsonElement CreateJsonElementFromArrayElementField(AssetsFieldInfo field)
    {
        string value = field.Value ?? throw new InvalidOperationException(
            $"Array field '{field.Name}' contains a non-scalar element.");

        if (string.Equals(field.TypeName, "string", StringComparison.OrdinalIgnoreCase))
        {
            return JsonElementFactory.String(value);
        }

        if (IsBooleanType(field.TypeName))
        {
            if (bool.TryParse(value, out bool boolean))
            {
                return JsonElementFactory.Boolean(boolean);
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long booleanInteger))
            {
                return JsonElementFactory.Boolean(booleanInteger != 0);
            }
        }

        if (IsUnsignedIntegerType(field.TypeName) &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong unsignedInteger))
        {
            return JsonElementFactory.Number(unsignedInteger);
        }

        if (IsSignedIntegerType(field.TypeName) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signedInteger))
        {
            return JsonElementFactory.Number(signedInteger);
        }

        if (IsFloatingPointType(field.TypeName) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatingPoint))
        {
            return JsonElementFactory.Number(floatingPoint);
        }

        return JsonElementFactory.String(value);
    }

    private static bool IsBooleanType(string typeName)
    {
        return string.Equals(typeName, "bool", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeName, "boolean", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSignedIntegerType(string typeName)
    {
        return typeName.Equals("int", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("short", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("long", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("int", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("sint", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnsignedIntegerType(string typeName)
    {
        return typeName.Equals("byte", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("uint", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFloatingPointType(string typeName)
    {
        return string.Equals(typeName, "float", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeName, "double", StringComparison.OrdinalIgnoreCase);
    }
}
