using System.Text.Json.Serialization;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed record RepositoryDocument(int FormatVersion, string? RepositoryId);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(RepositoryDocument))]
[JsonSerializable(typeof(RepositoryTransaction))]
[JsonSerializable(typeof(RepositoryMetadata))]
[JsonSerializable(typeof(BaseCatalog))]
[JsonSerializable(typeof(BaseFileEntry))]
[JsonSerializable(typeof(PayloadBaseEntry))]
[JsonSerializable(typeof(LayerRecord))]
[JsonSerializable(typeof(LayerPackageInfo))]
internal sealed partial class RepositoryJsonContext : JsonSerializerContext;
