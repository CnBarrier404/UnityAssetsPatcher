using System.Text.Json.Serialization;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed record RepositoryDocument(int FormatVersion, string? RepositoryId);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(RepositoryDocument))]
internal sealed partial class RepositoryCatalogJsonContext : JsonSerializerContext;
