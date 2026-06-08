using System.Text.Json;

namespace UnityAssetsPatcher.Core.Assets;

public sealed record FieldPatchOperation(string Path, JsonElement To);
