using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public sealed record AssetQueryConfig(
    string Name,
    string Author,
    string Version,
    string? Description,
    IReadOnlyList<AssetPatchTarget> Targets);

public sealed record AssetPatchTarget(
    string Target,
    string Type,
    IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> IncludeGroups,
    IReadOnlyList<PatchSetOperation>? SetOperations);

public sealed record PatchSetOperation(string Path, JsonElement From, JsonElement To);
