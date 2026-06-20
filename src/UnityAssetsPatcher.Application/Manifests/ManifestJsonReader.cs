using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Manifests;

public static class ManifestJsonReader
{
    private const long MaxManifestSize = 10 * 1024 * 1024; // 10MB

    public static JsonElement Read(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Manifest file not found: {configPath}", configPath);
        }

        return Path.GetExtension(configPath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? ReadManifestElementFromZip(configPath)
            : JsonUtils.ReadElementFromFile(configPath);
    }

    private static JsonElement ReadManifestElementFromZip(string zipPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        return ReadManifestElementFromZip(archive, zipPath);
    }

    public static JsonElement ReadManifestElementFromZip(ZipArchive archive, string zipPath)
    {
        var manifests = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) &&
                            string.Equals(entry.Name, "manifest.json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (manifests.Length != 1)
        {
            throw new InvalidOperationException(
                $"Zip file must contain exactly one manifest.json entry: {zipPath}");
        }

        ZipArchiveEntry manifest = manifests[0];

        if (manifest.Length > MaxManifestSize)
        {
            throw new InvalidOperationException(
                $"Manifest file '{manifest.FullName}' in '{zipPath}' exceeds maximum allowed size of {MaxManifestSize} bytes.");
        }

        using Stream stream = manifest.Open();
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}
