using System.Text.Json.Serialization;

namespace UnityAssetsPatcher.Application.Backups;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(InstallRecord))]
internal sealed partial class ModInstallationJsonContext : JsonSerializerContext;
