using System.Text.Json;

namespace UnityAssetsPatcher.Application.Assets;

public sealed record FieldPatchOperation(string Path, JsonElement To);
