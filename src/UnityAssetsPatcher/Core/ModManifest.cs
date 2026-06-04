using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public sealed record ModManifest(
    string Name,
    string Author,
    string Version,
    string? Description,
    IReadOnlyList<ManifestPatch> Patches);

public sealed record ManifestPatch(
    string AssetsFileName,
    string AssetTypeName,
    IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> IncludeGroups,
    IReadOnlyList<ManifestSetOperation>? SetOperations,
    IReadOnlyList<ManifestAddOperation>? AddOperations);

public sealed record ManifestSetOperation(string FieldPath, JsonElement From, JsonElement To);

public sealed record ManifestAddOperation(string FieldPath, JsonElement Value);
