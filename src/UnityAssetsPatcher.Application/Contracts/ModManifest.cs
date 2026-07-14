using System.Text.Json;

namespace UnityAssetsPatcher.Application.Contracts;

public sealed record ModManifest(
    int SchemaVersion,
    string Name,
    string Author,
    string Version,
    string? Description,
    string? Game,
    IReadOnlyList<ManifestFile> Files,
    IReadOnlyList<ManifestPatch> Patches,
    IReadOnlyList<ManifestOptionalGroup> Optional);

public sealed record ManifestOptionalGroup(
    string Name,
    string? Description,
    IReadOnlyList<ManifestFile> Files,
    IReadOnlyList<ManifestPatch> Patches);

public sealed record ManifestFile(string Source);

public sealed record ManifestPatch(
    string AssetsFileName,
    string AssetTypeName,
    IReadOnlyDictionary<string, JsonElement> Match,
    IReadOnlyList<ManifestSetOperation>? SetOperations,
    IReadOnlyList<ManifestAddOperation>? AddOperations,
    ManifestReplaceFrom? ReplaceFrom = null,
    string? ComponentTypeName = null);

public sealed record ManifestSetOperation(string FieldPath, JsonElement From, JsonElement To);

public sealed record ManifestAddOperation(string FieldPath, JsonElement Value);

public sealed record ManifestReplaceFrom(string AssetsFilePath, string MatchFieldPath);
