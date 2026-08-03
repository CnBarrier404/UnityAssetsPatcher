using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UnityAssetsPatcher.Domain.Json;
using LegacyModManifest = UnityAssetsPatcher.Application.Contracts.ModManifest;
using NewOperations = UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Manifests;

public sealed class ModManifestReader
{
    public LegacyModManifest Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Manifest file not found: {configPath}", configPath);
        }

        JsonElement manifestElement;

        if (Path.GetExtension(configPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using ZipArchive archive = ZipFile.OpenRead(configPath);
            manifestElement = ModManifestJsonReader.ReadFromZipArchive(archive, configPath);
        }
        else
        {
            manifestElement = JsonUtils.ReadElementFromFile(configPath);
        }

        return Load(manifestElement);
    }

    public LegacyModManifest Load(JsonElement manifestElement)
    {
        byte[] utf8Json = Encoding.UTF8.GetBytes(manifestElement.GetRawText());
        NewOperations.OperationResult<ModManifest> result = ModManifestParser.Parse(utf8Json);

        if (result is NewOperations.OperationFailed<ModManifest> failure)
        {
            throw new InvalidOperationException($"Manifest validation failed: {failure.Error.Code.Value}");
        }

        ModManifest manifest = ((NewOperations.OperationSucceeded<ModManifest>)result).Value;

        return LegacyModManifestMapper.Map(manifest);
    }
}
