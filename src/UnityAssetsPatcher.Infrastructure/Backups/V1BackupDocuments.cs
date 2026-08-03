using System.Text.Json.Serialization;

namespace UnityAssetsPatcher.Infrastructure.Backups;

internal sealed record V1RepositoryDocument(int FormatVersion, string? RepositoryId);

internal sealed record V1InstallRecordDocument(
    string? RepositoryId,
    string? GameInstanceFingerprint,
    long InstallSequence,
    string? Id,
    DateTimeOffset InstalledAt,
    string? ModName,
    string? ModVersion,
    string? ModAuthor,
    string? GameName,
    IReadOnlyList<V1InstallRecordPatchedFileDocument?>? PatchedFiles,
    IReadOnlyList<V1InstallRecordCopiedFileDocument?>? CopiedFiles)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string?>? OptionalGroups { get; init; }
}

internal sealed record V1InstallRecordPatchedFileDocument(
    string? Target,
    string? AssetsFileRelativePath,
    string? BackupRelativePath,
    int AssetCount,
    int OperationCount,
    V1FileIntegrityDocument? InstalledFile,
    V1FileIntegrityDocument? BackupFile);

internal sealed record V1InstallRecordCopiedFileDocument(
    string? Source,
    string? DestinationRelativePath,
    V1FileIntegrityDocument? InstalledFile);

internal sealed record V1FileIntegrityDocument(long Length, string? Sha256);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(V1RepositoryDocument))]
[JsonSerializable(typeof(V1InstallRecordDocument))]
internal sealed partial class V1BackupJsonContext : JsonSerializerContext;
