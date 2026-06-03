using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public sealed record AssetQueryConfig(
    string Type,
    IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> IncludeGroups,
    IReadOnlyList<PatchSetOperation>? SetOperations);

public sealed record PatchSetOperation(string Path, JsonElement From, JsonElement To);
