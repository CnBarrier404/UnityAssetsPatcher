using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Utils;

namespace UnityAssetsPatcher.Core;

internal static class ManifestJsonReader
{
    public static JsonElement Read(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Manifest file not found: {configPath}", configPath);
        }

        // A manifest can come directly from a JSON file or from manifest.json in a mod zip.
        return Path.GetExtension(configPath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? ReadManifestElementFromZip(configPath)
            : JsonUtils.ReadElementFromFile(configPath);
    }

    private static JsonElement ReadManifestElementFromZip(string zipPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        // Require exactly one manifest.json so zip packages are deterministic.
        var manifests = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) &&
                            string.Equals(entry.Name, "manifest.json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (manifests.Length != 1)
        {
            throw new InvalidOperationException(
                $"Zip file must contain exactly one manifest.json entry: {zipPath}");
        }

        using Stream stream = manifests[0].Open();
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}
