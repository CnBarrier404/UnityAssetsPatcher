using System.Text.Json;

namespace UnityAssetsPatcher.Domain.Assets;

public sealed record FieldPatchOperation(string Path, JsonElement To);
