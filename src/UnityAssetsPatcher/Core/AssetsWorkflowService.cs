using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public sealed class AssetsWorkflowService
{
    private readonly IAssetsReader _assetsReader;
    private readonly IAssetsPatchWriter? _assetsPatchWriter;

    public AssetsWorkflowService(IAssetsReader assetsReader)
        : this(assetsReader, null) { }

    public AssetsWorkflowService(IAssetsReader assetsReader, IAssetsPatchWriter? assetsPatchWriter)
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

        foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(request.AssetsFilePath,
                     queryConfig))
        {
            var includeGroup = queryConfig.IncludeGroups
                .First(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));
            matches.Add(new AssetMatch(asset, includeGroup));
        }

        return matches;
    }

    public PatchPreviewResult PreviewPatch(PatchPreviewRequest request)
    {
        AssetQueryConfig queryConfig = AssetQueryConfigLoader.Load(request.ConfigPath);

        if (queryConfig.SetOperations is null || queryConfig.SetOperations.Count == 0)
        {
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' array.");
        }

        var assets = new List<PatchPreviewAssetResult>();

        foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(request.AssetsFilePath,
                     queryConfig))
        {
            var operationResults = new List<PatchPreviewOperationResult>();

            foreach (PatchSetOperation operation in queryConfig.SetOperations)
            {
                AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.Path);
                string oldValue = field?.Value ?? "<missing>";
                bool matches = field?.Value is not null && AssetFieldMatcher.MatchesValue(field.Value, operation.From);

                operationResults.Add(new PatchPreviewOperationResult(
                    operation.Path,
                    oldValue,
                    operation.From,
                    operation.To,
                    matches));
            }

            assets.Add(new PatchPreviewAssetResult(asset, operationResults));
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

        if (queryConfig.SetOperations is null || queryConfig.SetOperations.Count == 0)
        {
            throw new InvalidOperationException("Patch config must contain a non-empty 'set' array.");
        }

        IReadOnlyList<PatchWriteAsset> plan = CreatePatchWritePlan(request.AssetsFilePath, queryConfig);

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

    private IEnumerable<(AssetsInfo Asset, AssetsFieldInfo FieldTree)> FindMatchingAssets(string assetsFilePath,
        AssetQueryConfig queryConfig)
    {
        var assets = _assetsReader.ReadAssetsInfo(assetsFilePath)
            .Where(asset => string.Equals(asset.TypeName, queryConfig.Type, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (AssetsInfo asset in assets)
        {
            AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(assetsFilePath, asset.PathId);
            bool matches =
                queryConfig.IncludeGroups.Any(group => AssetFieldMatcher.MatchesIncludeGroup(fieldTree, group));

            if (matches)
            {
                yield return (asset, fieldTree);
            }
        }
    }

    private IReadOnlyList<PatchWriteAsset> CreatePatchWritePlan(string assetsFilePath, AssetQueryConfig queryConfig)
    {
        if (queryConfig.SetOperations is null)
        {
            return [];
        }

        var assets = new List<PatchWriteAsset>();

        foreach ((AssetsInfo asset, AssetsFieldInfo fieldTree) in FindMatchingAssets(assetsFilePath, queryConfig))
        {
            var operations = new List<PatchWriteOperation>();

            foreach (PatchSetOperation operation in queryConfig.SetOperations)
            {
                EnsureSupportedPatchValue(operation.To, operation.Path);

                AssetsFieldInfo? field = AssetFieldMatcher.FindField(fieldTree, operation.Path);
                string oldValue = field?.Value ?? "<missing>";

                if (field?.Value is null || !AssetFieldMatcher.MatchesValue(field.Value, operation.From))
                {
                    throw new InvalidOperationException(
                        $"Patch operation cannot be applied for Path ID {asset.PathId}, field '{operation.Path}': current value {oldValue} does not match expected {AssetFieldMatcher.FormatJsonValue(operation.From)}.");
                }

                operations.Add(new PatchWriteOperation(operation.Path, oldValue, operation.To));
            }

            assets.Add(new PatchWriteAsset(asset.PathId, operations));
        }

        return assets;
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

        for (var index = 1; File.Exists(candidate); index++)
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
