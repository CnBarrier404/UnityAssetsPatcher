using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Manifests;

public static class ManifestJsonReader
{
    private const long MaxManifestSize = 10 * 1024 * 1024; // 10MB

    public static JsonElement ReadFromZipArchive(ZipArchive archive, string zipPath)
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

        return JsonUtils.ReadElementFromStream(stream, $"manifest file '{manifest.FullName}' in '{zipPath}'");
    }
}
