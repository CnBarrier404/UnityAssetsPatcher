using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Manifests;

public sealed class ModManifestReader
{
    public ModManifest Load(string configPath)
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

    public ModManifest Load(JsonElement manifestElement)
    {
        return ModManifestParser.Parse(manifestElement);
    }
}
