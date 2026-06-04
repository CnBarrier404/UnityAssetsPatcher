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
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' array.");
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

        ModManifest manifest = ModManifestLoader.Load(request.ConfigPath);
        var targets = GetPatchesForAssetsFile(manifest, request.AssetsFilePath);

        return ApplyPatchTargets(request.AssetsFilePath, request.OutputPath, request.BackupDirectory, targets);
    }

    public InstallModResult InstallMod(InstallModRequest request)
    {
        if (_assetsPatchWriter is null)
        {
            throw new InvalidOperationException("Assets patch writer was not configured.");
        }

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
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' array.");
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
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' array.");
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

        if (overwritesInput)
        {
            Directory.CreateDirectory(backupDirectory);
            backupPath = CreateBackupPath(backupDirectory, assetsFilePath);
            File.Copy(assetsFilePath, backupPath, false);
        }

        _assetsPatchWriter!.WritePatch(assetsFilePath, outputPath, plan);

        return new PatchApplyResult(
            outputPath,
            backupPath,
            plan.Count,
            plan.Sum(asset => asset.Operations.Count));
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
        var assets = _assetsReader.ReadAssetsInfo(assetsFilePath)
            .Where(asset => string.Equals(asset.TypeName, patch.AssetTypeName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (AssetsInfo asset in assets)
        {
            AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(assetsFilePath, asset.PathId);
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
            }
        }

        return operationGroups
            .Select(group => new PatchWriteAsset(group.Key, group.Value))
            .ToArray();
    }

    private static bool HasPatchOperations(IReadOnlyList<ManifestPatch> targets)
    {
        return targets.Count > 0 && targets.All(target => target.SetOperations is { Count: > 0 });
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
            string oldValue = field?.Value ?? "<missing>";
            bool matches = field is not null && AssetFieldMatcher.MatchesFieldValue(field, operation.From);

            return
            [
                new PatchPreviewOperationResult(
                    operation.FieldPath,
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
