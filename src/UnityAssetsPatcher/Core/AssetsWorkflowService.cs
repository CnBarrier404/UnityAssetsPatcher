using System.Globalization;
using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public sealed class AssetsWorkflowService
{
    private readonly IAssetsFileService _assetsFileService;

    public AssetsWorkflowService(IAssetsFileService assetsFileService)
    {
        _assetsFileService = assetsFileService;
    }

    public IReadOnlyList<AssetsInfo> InspectList(InspectListRequest request)
    {
        return _assetsFileService.ReadAssetsInfo(request.AssetsFilePath);
    }

    public AssetsFieldInfo InspectFields(InspectFieldsRequest request)
    {
        return _assetsFileService.ReadAssetsFieldInfo(request.AssetsFilePath, request.PathId);
    }

    public IReadOnlyList<AssetMatch> FindAssets(FindAssetsRequest request)
    {
        ModManifest manifest = ModManifestLoader.Load(request.ConfigPath);
        var targets = GetPatchesForAssetsFile(manifest, request.AssetsFilePath);
        var matches = new List<AssetMatch>();

        foreach (ManifestPatch patch in targets)
        {
            foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(request.AssetsFilePath,
                         patch))
            {
                var includeGroup = patch.IncludeGroups
                    .First(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));
                matches.Add(new AssetMatch(asset, includeGroup));
            }
        }

        return matches;
    }

    public PatchPreviewResult PreviewPatch(PatchPreviewRequest request)
    {
        ModManifest manifest = ModManifestLoader.Load(request.ConfigPath);
        var targets = GetPatchesForAssetsFile(manifest, request.AssetsFilePath);

        return CreatePatchPreviewResult(request.AssetsFilePath, targets);
    }

    public InstallPreviewResult PreviewInstallMod(InstallPreviewRequest request)
    {
        if (!File.Exists(request.ZipFilePath))
        {
            throw new FileNotFoundException($"Mod zip file not found: {request.ZipFilePath}", request.ZipFilePath);
        }

        if (!Directory.Exists(request.GameDirectory))
        {
            throw new DirectoryNotFoundException($"Game directory not found: {request.GameDirectory}");
        }

        ModManifest manifest = ModManifestLoader.Load(request.ZipFilePath);
        var targetPaths = ResolveInstallTargetPaths(
            request.GameDirectory,
            manifest.Patches.Select(patch => patch.AssetsFileName));
        var fileResults = new List<InstallPreviewFileResult>();

        foreach (var targetGroup in manifest.Patches
                     .GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase))
        {
            string assetsFilePath = targetPaths[targetGroup.Key];
            var targets = targetGroup.ToArray();
            PatchPreviewResult preview = CreatePatchPreviewResult(assetsFilePath, targets);
            fileResults.Add(new InstallPreviewFileResult(targetGroup.Key, assetsFilePath, preview));
        }

        return new InstallPreviewResult(manifest.Name, manifest.Version, fileResults);
    }

    private PatchPreviewResult CreatePatchPreviewResult(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets)
    {
        if (targets.Count == 0)
        {
            return new PatchPreviewResult([]);
        }

        if (!HasPatchOperations(targets))
        {
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' or 'add' array.");
        }

        var assets = new List<PatchPreviewAssetResult>();

        foreach (ManifestPatch patch in targets)
        {
            foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(assetsFilePath,
                         patch))
            {
                var operationResults = new List<PatchPreviewOperationResult>();

                foreach (ManifestSetOperation operation in patch.SetOperations ?? [])
                {
                    operationResults.AddRange(CreatePatchPreviewOperationResults(
                        assetsFilePath,
                        fieldTree,
                        operation));
                }

                foreach (ManifestAddOperation operation in patch.AddOperations ?? [])
                {
                    operationResults.AddRange(CreatePatchPreviewOperationResults(fieldTree, operation));
                }

                assets.Add(new PatchPreviewAssetResult(asset, operationResults));
            }
        }

        return new PatchPreviewResult(assets);
    }

    public PatchApplyResult ApplyPatch(PatchApplyRequest request)
    {
        ModManifest manifest = ModManifestLoader.Load(request.ConfigPath);
        var targets = GetPatchesForAssetsFile(manifest, request.AssetsFilePath);

        return ApplyPatchTargets(request.AssetsFilePath, request.OutputPath, request.BackupDirectory, targets);
    }

    public InstallModResult InstallMod(InstallModRequest request)
    {
        if (!File.Exists(request.ZipFilePath))
        {
            throw new FileNotFoundException($"Mod zip file not found: {request.ZipFilePath}", request.ZipFilePath);
        }

        if (!Directory.Exists(request.GameDirectory))
        {
            throw new DirectoryNotFoundException($"Game directory not found: {request.GameDirectory}");
        }

        ModManifest manifest = ModManifestLoader.Load(request.ZipFilePath);

        if (!HasPatchOperations(manifest.Patches))
        {
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' or 'add' array.");
        }

        var targetPaths = ResolveInstallTargetPaths(
            request.GameDirectory,
            manifest.Patches.Select(patch => patch.AssetsFileName));
        var plans = new List<InstallFilePlan>();

        foreach (var targetGroup in manifest.Patches
                     .GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase))
        {
            string assetsFilePath = targetPaths[targetGroup.Key];
            var targets = targetGroup.ToArray();
            var plan = CreatePatchWritePlan(assetsFilePath, targets);

            if (plan.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Patch config for target '{targetGroup.Key}' did not match any assets.");
            }

            plans.Add(new InstallFilePlan(targetGroup.Key, assetsFilePath, plan));
        }

        var fileResults = new List<InstallModFileResult>();

        foreach (InstallFilePlan plan in plans)
        {
            PatchApplyResult result = WritePatchPlan(
                plan.AssetsFilePath,
                plan.AssetsFilePath,
                true,
                request.BackupDirectory,
                plan.Assets);
            if (result.OperationCount == 0)
            {
                continue;
            }

            string backupPath = result.BackupPath ??
                                throw new InvalidOperationException("Install patch did not create a backup.");
            fileResults.Add(new InstallModFileResult(
                plan.Target,
                result.OutputPath,
                backupPath,
                result.AssetCount,
                result.OperationCount));
        }

        return new InstallModResult(manifest.Name, manifest.Version, fileResults);
    }

    private PatchApplyResult ApplyPatchTargets(
        string assetsFilePath,
        string? outputPathOption,
        string backupDirectory,
        IReadOnlyList<ManifestPatch> targets)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        string outputPath = outputPathOption ?? assetsFilePath;
        bool overwritesInput = string.Equals(
            Path.GetFullPath(outputPath),
            Path.GetFullPath(assetsFilePath),
            StringComparison.OrdinalIgnoreCase);

        if (outputPathOption is not null && overwritesInput)
        {
            throw new InvalidOperationException("--output cannot point to the input assets file.");
        }

        if (!overwritesInput && File.Exists(outputPath))
        {
            throw new IOException($"Output file already exists: {outputPath}");
        }

        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Patch config did not contain a target for assets file: {Path.GetFileName(assetsFilePath)}");
        }

        if (!HasPatchOperations(targets))
        {
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' or 'add' array.");
        }

        var plan = CreatePatchWritePlan(assetsFilePath, targets);

        if (plan.Count == 0)
        {
            throw new InvalidOperationException("Patch config did not match any assets.");
        }

        return WritePatchPlan(assetsFilePath, outputPath, overwritesInput, backupDirectory, plan);
    }

    private PatchApplyResult WritePatchPlan(
        string assetsFilePath,
        string outputPath,
        bool overwritesInput,
        string backupDirectory,
        IReadOnlyList<PatchWriteAsset> plan)
    {
        string? backupPath = null;
        var changedPlan = plan
            .Where(asset => asset.Operations.Count > 0)
            .ToArray();

        if (changedPlan.Length == 0)
        {
            return new PatchApplyResult(outputPath, null, 0, 0);
        }

        if (overwritesInput)
        {
            Directory.CreateDirectory(backupDirectory);
            backupPath = CreateBackupPath(backupDirectory, assetsFilePath);
            File.Copy(assetsFilePath, backupPath, false);
        }

        _assetsFileService.WritePatch(assetsFilePath, outputPath, changedPlan);

        return new PatchApplyResult(
            outputPath,
            backupPath,
            changedPlan.Length,
            changedPlan.Sum(asset => asset.Operations.Count));
    }

    private static IReadOnlyList<ManifestPatch> GetPatchesForAssetsFile(
        ModManifest manifest,
        string assetsFilePath)
    {
        string fileName = Path.GetFileName(assetsFilePath);

        return manifest.Patches
            .Where(patch => string.Equals(patch.AssetsFileName, fileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ResolveInstallTargetPaths(
        string gameDirectory,
        IEnumerable<string> targets)
    {
        string fullGameDirectory = Path.GetFullPath(gameDirectory);
        var resolvedTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string target in targets.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string[] matches = Directory.EnumerateFiles(fullGameDirectory, "*", SearchOption.AllDirectories)
                .Where(file => string.Equals(Path.GetFileName(file), target, StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)
                .ToArray();

            if (matches.Length == 0)
            {
                throw new FileNotFoundException(
                    $"Target '{target}' was not found under game directory: {fullGameDirectory}",
                    target);
            }

            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Target '{target}' matched multiple files under game directory: {fullGameDirectory}");
            }

            resolvedTargets.Add(target, matches[0]);
        }

        return resolvedTargets;
    }

    private IEnumerable<(AssetsInfo Asset, AssetsFieldInfo FieldTree)> FindMatchingAssets(
        string assetsFilePath,
        ManifestPatch patch)
    {
        var assets = _assetsFileService.ReadAssetsInfo(assetsFilePath)
            .Where(asset => string.Equals(asset.TypeName, patch.AssetTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (AssetsInfo asset in assets)
        {
            AssetsFieldInfo fieldTree = _assetsFileService.ReadAssetsFieldInfo(assetsFilePath, asset.PathId);
            bool matches =
                patch.IncludeGroups.Any(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));

            if (matches)
            {
                yield return (asset, fieldTree);
            }
        }
    }

    private IReadOnlyList<PatchWriteAsset> CreatePatchWritePlan(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets)
    {
        if (!HasPatchOperations(targets))
        {
            return [];
        }

        var operationGroups = new Dictionary<long, List<PatchWriteOperation>>();

        foreach (ManifestPatch patch in targets)
        {
            foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(assetsFilePath, patch))
            {
                if (!operationGroups.TryGetValue(asset.PathId, out var operations))
                {
                    operations = [];
                    operationGroups.Add(asset.PathId, operations);
                }

                foreach (ManifestSetOperation operation in patch.SetOperations ?? [])
                {
                    operations.AddRange(CreatePatchWriteOperations(
                        assetsFilePath,
                        asset.PathId,
                        fieldTree,
                        operation));
                }

                foreach (ManifestAddOperation operation in patch.AddOperations ?? [])
                {
                    operations.AddRange(CreatePatchWriteOperations(asset.PathId, fieldTree, operation));
                }
            }
        }

        return operationGroups
            .Select(group => new PatchWriteAsset(group.Key, group.Value))
            .ToArray();
    }

    private static bool HasPatchOperations(IReadOnlyList<ManifestPatch> targets)
    {
        return targets.Count > 0 && targets.All(HasPatchOperations);
    }

    private static bool HasPatchOperations(ManifestPatch target)
    {
        return target.SetOperations is { Count: > 0 } ||
               target.AddOperations is { Count: > 0 };
    }

    private IReadOnlyList<PatchPreviewOperationResult> CreatePatchPreviewOperationResults(
        string assetsFilePath,
        AssetsFieldInfo fieldTree,
        ManifestSetOperation operation)
    {
        operation = ResolveManifestSetOperation(assetsFilePath, operation);
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.FieldPath);

        if (!AssetFieldMatcher.TryGetObjectValue(operation.To, out JsonElement toObject))
        {
            string path = operation.FieldPath;
            string oldValue = field?.Value ?? "<missing>";

            if (IsJsonArrayPatchValue(operation.To))
            {
                AssetsFieldInfo? arrayField = ResolveArrayField(field);
                path = ResolveArrayFieldPath(operation.FieldPath, field, arrayField);
                oldValue = arrayField is null ? "<missing>" : FormatArrayFieldValue(arrayField);
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
            AssetsFieldInfo? child = FindDirectChild(field, property.Name);
            string childPath = $"{operation.FieldPath}.{property.Name}";
            bool isArrayPatch = IsJsonArrayPatchValue(property.Value);
            string oldValue = isArrayPatch && child is not null
                ? FormatArrayFieldValue(child)
                : child?.Value ?? "<missing>";

            results.Add(new PatchPreviewOperationResult(
                childPath,
                oldValue,
                GetObjectPropertyOrDefault(operation.From, property.Name),
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
        operation = ResolveManifestSetOperation(assetsFilePath, operation);
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.FieldPath);

        if (!AssetFieldMatcher.TryGetObjectValue(operation.To, out JsonElement toObject))
        {
            if (IsJsonArrayPatchValue(operation.To))
            {
                EnsureSupportedPatchArrayValue(operation.To, operation.FieldPath);
                AssetsFieldInfo? arrayField = ResolveArrayField(field);
                string path = ResolveArrayFieldPath(operation.FieldPath, field, arrayField);
                string arrayOldValue = arrayField is null ? "<missing>" : FormatArrayFieldValue(arrayField);

                if (field is null || arrayField is null || !AssetFieldMatcher.MatchesFieldValue(field, operation.From))
                {
                    throw new InvalidOperationException(
                        $"Patch operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': current value {arrayOldValue} does not match expected {AssetFieldMatcher.FormatJsonValue(operation.From)}.");
                }

                return [new PatchWriteOperation(path, arrayOldValue, operation.To.Clone())];
            }

            EnsureSupportedPatchValue(operation.To, operation.FieldPath);
            string oldValue = field?.Value ?? "<missing>";

            if (field is null || !AssetFieldMatcher.MatchesFieldValue(field, operation.From))
            {
                throw new InvalidOperationException(
                    $"Patch operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': current value {oldValue} does not match expected {AssetFieldMatcher.FormatJsonValue(operation.From)}.");
            }

            return [new PatchWriteOperation(operation.FieldPath, oldValue, operation.To)];
        }

        string compositeOldValue = field is null ? "<missing>" : FormatObjectFieldValue(field);

        if (field is null || !AssetFieldMatcher.MatchesFieldValue(field, operation.From))
        {
            throw new InvalidOperationException(
                $"Patch operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': current value {compositeOldValue} does not match expected {AssetFieldMatcher.FormatJsonValue(operation.From)}.");
        }

        var operations = new List<PatchWriteOperation>();

        foreach (JsonProperty property in toObject.EnumerateObject())
        {
            string childPath = $"{operation.FieldPath}.{property.Name}";

            AssetsFieldInfo child = FindDirectChild(field, property.Name)
                                    ?? throw new InvalidOperationException(
                                        $"Field not found for Path ID {pathId}: {childPath}");

            if (IsJsonArrayPatchValue(property.Value))
            {
                EnsureSupportedPatchArrayValue(property.Value, childPath);
                operations.Add(new PatchWriteOperation(
                    childPath,
                    FormatArrayFieldValue(child),
                    property.Value.Clone()));
                continue;
            }

            EnsureSupportedPatchValue(property.Value, childPath);

            string oldValue = child.Value ?? throw new InvalidOperationException(
                $"Patch operation cannot be applied for Path ID {pathId}, field '{childPath}': current value <missing> does not match expected {AssetFieldMatcher.FormatJsonValue(GetObjectPropertyOrDefault(operation.From, property.Name))}.");

            operations.Add(new PatchWriteOperation(childPath, oldValue, property.Value.Clone()));
        }

        return operations;
    }

    private static IReadOnlyList<PatchPreviewOperationResult> CreatePatchPreviewOperationResults(
        AssetsFieldInfo fieldTree,
        ManifestAddOperation operation)
    {
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.FieldPath);
        AssetsFieldInfo? arrayField = ResolveArrayField(field);
        string path = ResolveArrayFieldPath(operation.FieldPath, field, arrayField);

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

        EnsureSupportedPatchArrayValue(operation.Value, operation.FieldPath);
        JsonElement to = CreateAddArrayValue(arrayField, operation.Value, out bool willChange);

        return
        [
            new PatchPreviewOperationResult(
                path,
                FormatArrayFieldValue(arrayField),
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
        AssetsFieldInfo? arrayField = ResolveArrayField(field);
        string path = ResolveArrayFieldPath(operation.FieldPath, field, arrayField);

        if (field is null || arrayField is null)
        {
            throw new InvalidOperationException(
                $"Patch add operation cannot be applied for Path ID {pathId}, field '{operation.FieldPath}': field is not an array.");
        }

        EnsureSupportedPatchArrayValue(operation.Value, operation.FieldPath);
        string oldValue = FormatArrayFieldValue(arrayField);
        JsonElement to = CreateAddArrayValue(arrayField, operation.Value, out bool willChange);

        return willChange ? [new PatchWriteOperation(path, oldValue, to)] : [];
    }

    private ManifestSetOperation ResolveManifestSetOperation(string assetsFilePath, ManifestSetOperation operation)
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
        return JsonSerializer.SerializeToElement(pathId);
    }

    private long ResolvePathIdReference(string assetsFilePath, JsonElement resolver)
    {
        string type = ReadRequiredPathIdResolverString(resolver, "type");
        var includeGroups =
            ReadPathIdResolverIncludeGroups(resolver);

        var target = new ManifestPatch(
            Path.GetFileName(assetsFilePath),
            type,
            includeGroups,
            null,
            null);
        var matches = FindMatchingAssets(assetsFilePath, target)
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

    private static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadPathIdResolverIncludeGroups(
        JsonElement resolver)
    {
        if (!resolver.TryGetProperty("include", out JsonElement includeElement) ||
            includeElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Path ID reference must contain an 'include' array.");
        }

        var includeGroups = new List<IReadOnlyDictionary<string, JsonElement>>();

        foreach (JsonElement includeGroupElement in includeElement.EnumerateArray())
        {
            if (includeGroupElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Each Path ID reference include entry must be an object.");
            }

            includeGroups.Add(includeGroupElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal));
        }

        return includeGroups.Count == 0
            ? throw new InvalidOperationException("Path ID reference include array cannot be empty.")
            : includeGroups;
    }

    private static AssetsFieldInfo? FindDirectChild(AssetsFieldInfo field, string name)
    {
        return field.Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.Ordinal));
    }

    private static bool IsJsonArrayPatchValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Array &&
               !AssetFieldMatcher.TryGetObjectValue(value, out _);
    }

    private static AssetsFieldInfo? ResolveArrayField(AssetsFieldInfo? field)
    {
        if (field is null)
        {
            return null;
        }

        if (IsArrayField(field))
        {
            return field;
        }

        return FindDirectChild(field, "Array");
    }

    private static string ResolveArrayFieldPath(
        string fieldPath,
        AssetsFieldInfo? field,
        AssetsFieldInfo? arrayField)
    {
        return field is not null && arrayField is not null && !ReferenceEquals(field, arrayField)
            ? $"{fieldPath}.{arrayField.Name}"
            : fieldPath;
    }

    private static bool IsArrayField(AssetsFieldInfo field)
    {
        return string.Equals(field.Name, "Array", StringComparison.Ordinal) ||
               string.Equals(field.TypeName, "Array", StringComparison.Ordinal);
    }

    private static IReadOnlyList<AssetsFieldInfo> GetArrayElementFields(AssetsFieldInfo arrayField)
    {
        var dataChildren = arrayField.Children
            .Where(child => string.Equals(child.Name, "data", StringComparison.Ordinal))
            .ToArray();

        return dataChildren.Length > 0 ? dataChildren : arrayField.Children;
    }

    private static JsonElement GetObjectPropertyOrDefault(JsonElement value, string propertyName)
    {
        return AssetFieldMatcher.TryGetObjectValue(value, out JsonElement objectValue) &&
               objectValue.TryGetProperty(propertyName, out JsonElement propertyValue)
            ? propertyValue.Clone()
            : value;
    }

    private static string FormatObjectFieldValue(AssetsFieldInfo field)
    {
        string properties = string.Join(", ", field.Children
            .Where(child => child.Value is not null)
            .Select(child => $"{child.Name}: {child.Value}"));

        return properties.Length == 0 ? "<missing>" : $"{{ {properties} }}";
    }

    private static string FormatArrayFieldValue(AssetsFieldInfo arrayField)
    {
        string elements = string.Join(", ", GetArrayElementFields(arrayField).Select(FormatArrayElementValue));

        return $"[{elements}]";
    }

    private static string FormatArrayElementValue(AssetsFieldInfo element)
    {
        if (element.Value is null)
        {
            return FormatObjectFieldValue(element);
        }

        return string.Equals(element.TypeName, "string", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Serialize(element.Value)
            : element.Value;
    }

    private static JsonElement CreateAddArrayValue(
        AssetsFieldInfo arrayField,
        JsonElement value,
        out bool changed)
    {
        var currentFields = GetArrayElementFields(arrayField);
        var elements = currentFields
            .Select(CreateJsonElementFromArrayElementField)
            .ToList();
        changed = false;

        foreach (JsonElement element in value.EnumerateArray())
        {
            if (ContainsArrayValue(currentFields, elements, element))
            {
                continue;
            }

            elements.Add(element.Clone());
            changed = true;
        }

        return JsonSerializer.SerializeToElement(elements);
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
               string.Equals(AssetFieldMatcher.FormatJsonValue(left), AssetFieldMatcher.FormatJsonValue(right),
                   StringComparison.Ordinal);
    }

    private static JsonElement CreateJsonElementFromArrayElementField(AssetsFieldInfo field)
    {
        string value = field.Value ?? throw new InvalidOperationException(
            $"Array field '{field.Name}' contains a non-scalar element.");

        if (string.Equals(field.TypeName, "string", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.SerializeToElement(value);
        }

        if (IsBooleanType(field.TypeName))
        {
            if (bool.TryParse(value, out bool boolean))
            {
                return JsonSerializer.SerializeToElement(boolean);
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long booleanInteger))
            {
                return JsonSerializer.SerializeToElement(booleanInteger != 0);
            }
        }

        if (IsUnsignedIntegerType(field.TypeName) &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong unsignedInteger))
        {
            return JsonSerializer.SerializeToElement(unsignedInteger);
        }

        if (IsSignedIntegerType(field.TypeName) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signedInteger))
        {
            return JsonSerializer.SerializeToElement(signedInteger);
        }

        if (IsFloatingPointType(field.TypeName) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatingPoint))
        {
            return JsonSerializer.SerializeToElement(floatingPoint);
        }

        return JsonSerializer.SerializeToElement(value);
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

    private static void EnsureSupportedPatchValue(JsonElement value, string path)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number
            or JsonValueKind.String)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Patch operation for field '{path}' uses an unsupported value type: {value.ValueKind}.");
    }

    private static void EnsureSupportedPatchArrayValue(JsonElement value, string path)
    {
        int index = 0;

        foreach (JsonElement element in value.EnumerateArray())
        {
            EnsureSupportedPatchValue(element, $"{path}[{index}]");
            index++;
        }
    }

    private static string CreateBackupPath(string backupDirectory, string inputPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(inputPath);
        string extension = Path.GetExtension(inputPath);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string candidate = Path.Combine(backupDirectory, $"{fileName}.{timestamp}{extension}");

        for (int index = 1; File.Exists(candidate); index++)
        {
            candidate = Path.Combine(backupDirectory, $"{fileName}.{timestamp}.{index}{extension}");
        }

        return candidate;
    }

    private sealed record InstallFilePlan(
        string Target,
        string AssetsFilePath,
        IReadOnlyList<PatchWriteAsset> Assets);
}

public sealed record InspectListRequest(string AssetsFilePath, int? Limit);

public sealed record InspectFieldsRequest(string AssetsFilePath, long PathId);

public sealed record FindAssetsRequest(string AssetsFilePath, string ConfigPath);

public sealed record PatchPreviewRequest(string AssetsFilePath, string ConfigPath);

public sealed record PatchApplyRequest(
    string AssetsFilePath,
    string ConfigPath,
    string? OutputPath,
    string BackupDirectory);

public sealed record InstallModRequest(
    string ZipFilePath,
    string GameDirectory,
    string BackupDirectory);

public sealed record InstallPreviewRequest(string ZipFilePath, string GameDirectory);

public sealed record PatchApplyResult(string OutputPath, string? BackupPath, int AssetCount, int OperationCount);

public sealed record InstallModResult(
    string ModName,
    string ModVersion,
    IReadOnlyList<InstallModFileResult> Files);

public sealed record InstallModFileResult(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);

public sealed record InstallPreviewResult(
    string ModName,
    string ModVersion,
    IReadOnlyList<InstallPreviewFileResult> Files);

public sealed record InstallPreviewFileResult(
    string Target,
    string AssetsFilePath,
    PatchPreviewResult Preview);

public sealed record AssetMatch(AssetsInfo Asset, IReadOnlyDictionary<string, JsonElement> IncludeGroup);

public sealed record PatchPreviewResult(IReadOnlyList<PatchPreviewAssetResult> Assets);

public sealed record PatchPreviewAssetResult(AssetsInfo Asset, IReadOnlyList<PatchPreviewOperationResult> Operations);

public sealed record PatchPreviewOperationResult(
    string Path,
    string OldValue,
    JsonElement From,
    JsonElement To,
    bool WillChange);
