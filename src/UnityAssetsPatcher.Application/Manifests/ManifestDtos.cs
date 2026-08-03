using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnityAssetsPatcher.Application.Manifests;

internal sealed class ManifestDocumentDto
{
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Game { get; init; }
    public ManifestFileDto[]? CopyFiles { get; init; }
    public ManifestTargetDto[]? Targets { get; init; }
    public ManifestOptionalGroupDto[]? Optional { get; init; }
}

internal sealed class ManifestFileDto
{
    public string Source { get; init; } = string.Empty;
}

internal sealed class ManifestTargetDto
{
    public string File { get; init; } = string.Empty;
    public ManifestPatchDto[]? Patches { get; init; }
}

internal sealed class ManifestPatchDto
{
    public string Type { get; init; } = string.Empty;
    public JsonElement Match { get; init; }
    public string? ComponentType { get; init; }
    public JsonElement Set { get; init; }
    public JsonElement Add { get; init; }
    public ManifestReplaceAssetDto? ReplaceAsset { get; init; }
    public ManifestCopyAssetDto? CopyAsset { get; init; }
}

internal sealed class ManifestReplaceAssetDto
{
    public string FromFile { get; init; } = string.Empty;
    public string MatchField { get; init; } = string.Empty;
}

internal sealed class ManifestCopyAssetDto
{
    public ManifestCopyAssetSourceDto From { get; init; } = new();
}

internal sealed class ManifestCopyAssetSourceDto
{
    public string Type { get; init; } = string.Empty;
    public JsonElement Match { get; init; }
}

internal sealed class ManifestOptionalGroupDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ManifestFileDto[]? CopyFiles { get; init; }
    public ManifestTargetDto[]? Targets { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(ManifestDocumentDto))]
internal sealed partial class ManifestJsonSerializerContext : JsonSerializerContext { }
