using System.Text.Json;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record ModManifest(
    string Name,
    string Author,
    string Version,
    string? Description,
    string? Game,
    IReadOnlyList<ManifestFile> Files,
    IReadOnlyList<ManifestPatch> Patches);

public sealed record ManifestFile(string Source);

public sealed record ManifestPatch(
    string AssetsFileName,
    string AssetTypeName,
    IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> IncludeGroups,
    IReadOnlyList<ManifestSetOperation>? SetOperations,
    IReadOnlyList<ManifestAddOperation>? AddOperations,
    ManifestReplaceFrom? ReplaceFrom = null,
    string? ComponentTypeName = null);

public sealed record ManifestSetOperation(string FieldPath, JsonElement From, JsonElement To);

public sealed record ManifestAddOperation(string FieldPath, JsonElement Value);

public sealed record ManifestReplaceFrom(string AssetsFilePath, string MatchFieldPath);
