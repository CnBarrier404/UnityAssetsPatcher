using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public sealed class AssetsWorkflowService
{
    private readonly IAssetsReader _assetsReader;
    private readonly IAssetsPatchWriter? _assetsPatchWriter;

    public AssetsWorkflowService(IAssetsReader assetsReader, IAssetsPatchWriter? assetsPatchWriter = null)
    {
        _assetsReader = assetsReader;
        _assetsPatchWriter = assetsPatchWriter;
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

        foreach (AssetPatchTarget target in queryConfig.Targets)
        {
            foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(request.AssetsFilePath,
                         target))
            {
                var includeGroup = target.IncludeGroups
                    .First(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));
                matches.Add(new AssetMatch(asset, includeGroup));
            }
        }

        return matches;
    }

    public PatchPreviewResult PreviewPatch(PatchPreviewRequest request)
    {
        AssetQueryConfig queryConfig = AssetQueryConfigLoader.Load(request.ConfigPath);

        if (!HasPatchOperations(queryConfig))
        {
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' array.");
        }

        var assets = new List<PatchPreviewAssetResult>();

        foreach (AssetPatchTarget target in queryConfig.Targets)
        {
            foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(request.AssetsFilePath,
                         target))
            {
                var operationResults = new List<PatchPreviewOperationResult>();

                foreach (PatchSetOperation operation in target.SetOperations ?? [])
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
        if (_assetsPatchWriter is null)
        {
            throw new InvalidOperationException("Assets patch writer was not configured.");
        }

        if (!File.Exists(request.AssetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {request.AssetsFilePath}", request.AssetsFilePath);
        }

        string outputPath = request.OutputPath ?? request.AssetsFilePath;
        bool overwritesInput = string.Equals(
            Path.GetFullPath(outputPath),
            Path.GetFullPath(request.AssetsFilePath),
            StringComparison.OrdinalIgnoreCase);

        if (request.OutputPath is not null && overwritesInput)
        {
            throw new InvalidOperationException("--output cannot point to the input assets file.");
        }

        if (!overwritesInput && File.Exists(outputPath))
        {
            throw new IOException($"Output file already exists: {outputPath}");
        }

        AssetQueryConfig queryConfig = AssetQueryConfigLoader.Load(request.ConfigPath);

        if (!HasPatchOperations(queryConfig))
        {
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' array.");
        }

        var plan = CreatePatchWritePlan(request.AssetsFilePath, queryConfig);

        if (plan.Count == 0)
        {
            throw new InvalidOperationException("Patch config did not match any assets.");
        }

        string? backupPath = null;

        if (overwritesInput)
        {
            Directory.CreateDirectory(request.BackupDirectory);
            backupPath = CreateBackupPath(request.BackupDirectory, request.AssetsFilePath);
            File.Copy(request.AssetsFilePath, backupPath, false);
        }

        _assetsPatchWriter.WritePatch(request.AssetsFilePath, outputPath, plan);

        return new PatchApplyResult(
            outputPath,
            backupPath,
            plan.Count,
            plan.Sum(asset => asset.Operations.Count));
    }

    private IEnumerable<(AssetsInfo Asset, AssetsFieldInfo FieldTree)> FindMatchingAssets(
        string assetsFilePath,
        AssetPatchTarget target)
    {
        var assets = _assetsReader.ReadAssetsInfo(assetsFilePath)
            .Where(asset => string.Equals(asset.TypeName, target.Type, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (AssetsInfo asset in assets)
        {
            AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(assetsFilePath, asset.PathId);
            bool matches =
                target.IncludeGroups.Any(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));

            if (matches)
            {
                yield return (asset, fieldTree);
            }
        }
    }

    private IReadOnlyList<PatchWriteAsset> CreatePatchWritePlan(string assetsFilePath, AssetQueryConfig queryConfig)
    {
        if (!HasPatchOperations(queryConfig))
        {
            return [];
        }

        var operationGroups = new Dictionary<long, List<PatchWriteOperation>>();

        foreach (AssetPatchTarget target in queryConfig.Targets)
        {
            foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(assetsFilePath, target))
            {
                if (!operationGroups.TryGetValue(asset.PathId, out List<PatchWriteOperation>? operations))
                {
                    operations = [];
                    operationGroups.Add(asset.PathId, operations);
                }

                foreach (PatchSetOperation operation in target.SetOperations ?? [])
                {
                    operations.AddRange(CreatePatchWriteOperations(asset.PathId, fieldTree, operation));
                }
            }
        }

        return operationGroups
            .Select(group => new PatchWriteAsset(group.Key, group.Value))
            .ToArray();
    }

    private static bool HasPatchOperations(AssetQueryConfig queryConfig)
    {
        return queryConfig.Targets.All(target => target.SetOperations is { Count: > 0 });
    }

    private static IReadOnlyList<PatchPreviewOperationResult> CreatePatchPreviewOperationResults(
        AssetsFieldInfo fieldTree,
        PatchSetOperation operation)
    {
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.Path);

        if (!AssetFieldMatcher.TryGetObjectValue(operation.To, out JsonElement toObject))
        {
            string oldValue = field?.Value ?? "<missing>";
            bool matches = field is not null && AssetFieldMatcher.MatchesFieldValue(field, operation.From);

            return
            [
                new PatchPreviewOperationResult(
                    operation.Path,
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
                    operation.Path,
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
            string childPath = $"{operation.Path}.{property.Name}";
            string oldValue = child?.Value ?? "<missing>";

            results.Add(new PatchPreviewOperationResult(
                childPath,
                oldValue,
                GetObjectPropertyOrDefault(operation.From, property.Name),
                property.Value.Clone(),
                parentMatches && child?.Value is not null));
        }

        return results;
    }

    private static IReadOnlyList<PatchWriteOperation> CreatePatchWriteOperations(
        long pathId,
        AssetsFieldInfo fieldTree,
        PatchSetOperation operation)
    {
        AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.Path);

        if (!AssetFieldMatcher.TryGetObjectValue(operation.To, out JsonElement toObject))
        {
            EnsureSupportedPatchValue(operation.To, operation.Path);
            string oldValue = field?.Value ?? "<missing>";

            if (field is null || !AssetFieldMatcher.MatchesFieldValue(field, operation.From))
            {
                throw new InvalidOperationException(
                    $"Patch operation cannot be applied for Path ID {pathId}, field '{operation.Path}': current value {oldValue} does not match expected {AssetFieldMatcher.FormatJsonValue(operation.From)}.");
            }

            return [new PatchWriteOperation(operation.Path, oldValue, operation.To)];
        }

        string compositeOldValue = field is null ? "<missing>" : FormatObjectFieldValue(field);

        if (field is null || !AssetFieldMatcher.MatchesFieldValue(field, operation.From))
        {
            throw new InvalidOperationException(
                $"Patch operation cannot be applied for Path ID {pathId}, field '{operation.Path}': current value {compositeOldValue} does not match expected {AssetFieldMatcher.FormatJsonValue(operation.From)}.");
        }

        var operations = new List<PatchWriteOperation>();

        foreach (JsonProperty property in toObject.EnumerateObject())
        {
            string childPath = $"{operation.Path}.{property.Name}";
            EnsureSupportedPatchValue(property.Value, childPath);

            AssetsFieldInfo child = FindDirectChild(field, property.Name)
                                    ?? throw new InvalidOperationException(
                                        $"Field not found for Path ID {pathId}: {childPath}");

            string oldValue = child.Value ?? throw new InvalidOperationException(
                $"Patch operation cannot be applied for Path ID {pathId}, field '{childPath}': current value <missing> does not match expected {AssetFieldMatcher.FormatJsonValue(GetObjectPropertyOrDefault(operation.From, property.Name))}.");

            operations.Add(new PatchWriteOperation(childPath, oldValue, property.Value.Clone()));
        }

        return operations;
    }

    private static AssetsFieldInfo? FindDirectChild(AssetsFieldInfo field, string name)
    {
        return field.Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.Ordinal));
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

public sealed record PatchApplyResult(string OutputPath, string? BackupPath, int AssetCount, int OperationCount);

public sealed record AssetMatch(AssetsInfo Asset, IReadOnlyDictionary<string, JsonElement> IncludeGroup);

public sealed record PatchPreviewResult(IReadOnlyList<PatchPreviewAssetResult> Assets);

public sealed record PatchPreviewAssetResult(AssetsInfo Asset, IReadOnlyList<PatchPreviewOperationResult> Operations);

public sealed record PatchPreviewOperationResult(
    string Path,
    string OldValue,
    JsonElement From,
    JsonElement To,
    bool WillChange);
