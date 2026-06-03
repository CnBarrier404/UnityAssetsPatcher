using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Utils;

namespace UnityAssetsPatcher.Core;

public static class AssetQueryConfigLoader
{
    public static AssetQueryConfig Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Manifest file not found: {configPath}", configPath);
        }

        JsonElement root = Path.GetExtension(configPath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? ReadManifestElementFromZip(configPath)
            : JsonUtils.ReadElementFromFile(configPath);

        return Load(root);
    }

    private static AssetQueryConfig Load(JsonElement root)
    {
        string name = ReadRequiredMetadataString(root, "name");
        string author = ReadRequiredMetadataString(root, "author");
        string version = ReadRequiredMetadataString(root, "version");
        string? description = ReadOptionalMetadataString(root, "description");

        if (root.TryGetProperty("patches", out JsonElement patchesElement))
        {
            if (patchesElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Manifest 'patches' property must be an array.");
            }

            var targets = patchesElement.EnumerateArray()
                .Select(ReadPatchTarget)
                .ToArray();

            return targets.Length == 0
                ? throw new InvalidOperationException("Manifest patches array cannot be empty.")
                : new AssetQueryConfig(name, author, version, description, targets);
        }

        return new AssetQueryConfig(name, author, version, description, [ReadPatchTarget(root)]);
    }

    private static string ReadRequiredMetadataString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"Manifest must contain a non-empty string '{propertyName}' property.");
        }

        string? value = propertyElement.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Manifest must contain a non-empty string '{propertyName}' property.")
            : value;
    }

    private static string? ReadOptionalMetadataString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Manifest '{propertyName}' property must be a string.");
        }

        return propertyElement.GetString();
    }

    private static AssetPatchTarget ReadPatchTarget(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each patch target must be an object.");
        }

        string target = ReadTargetFileName(element);
        string type = element.TryGetProperty("type", out JsonElement typeElement) &&
                      typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? throw new InvalidOperationException("Manifest patch type cannot be empty.")
            : throw new InvalidOperationException("Manifest patch must contain a string 'type' property.");

        if (!element.TryGetProperty("include", out JsonElement includeElement) ||
            includeElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest patch must contain an 'include' array.");
        }

        var includeGroups = new List<IReadOnlyDictionary<string, JsonElement>>();

        foreach (JsonElement includeGroupElement in includeElement.EnumerateArray())
        {
            if (includeGroupElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Each include entry must be an object.");
            }

            includeGroups.Add(includeGroupElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal));
        }

        IReadOnlyList<PatchSetOperation>? setOperations = null;

        if (!element.TryGetProperty("set", out JsonElement setElement))
        {
            return includeGroups.Count == 0
                ? throw new InvalidOperationException("Manifest patch include array cannot be empty.")
                : new AssetPatchTarget(target, type, includeGroups, setOperations);
        }

        if (setElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest patch 'set' property must be an array.");
        }

        setOperations = setElement.EnumerateArray()
            .Select(ReadPatchSetOperation)
            .ToArray();

        return includeGroups.Count == 0
            ? throw new InvalidOperationException("Manifest patch include array cannot be empty.")
            : new AssetPatchTarget(target, type, includeGroups, setOperations);
    }

    private static PatchSetOperation ReadPatchSetOperation(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each set entry must be an object.");
        }

        if (element.TryGetProperty("path", out _))
        {
            throw new InvalidOperationException(
                "Each set entry must use a string 'field' property; 'path' is not supported.");
        }

        if (!element.TryGetProperty("field", out JsonElement fieldElement) ||
            fieldElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Each set entry must contain a string 'field' property.");
        }

        if (!element.TryGetProperty("from", out JsonElement fromElement))
        {
            throw new InvalidOperationException("Each set entry must contain a 'from' property.");
        }

        if (!element.TryGetProperty("to", out JsonElement toElement))
        {
            throw new InvalidOperationException("Each set entry must contain a 'to' property.");
        }

        string field = fieldElement.GetString() ?? throw new InvalidOperationException("Set field cannot be empty.");
        return new PatchSetOperation(field, fromElement.Clone(), toElement.Clone());
    }

    private static string ReadTargetFileName(JsonElement element)
    {
        if (!element.TryGetProperty("target", out JsonElement targetElement) ||
            targetElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Manifest patch must contain a string 'target' property.");
        }

        string? target = targetElement.GetString();

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("Manifest patch 'target' property cannot be empty.");
        }

        if (Path.IsPathRooted(target) ||
            target.Contains('/', StringComparison.Ordinal) ||
            target.Contains('\\', StringComparison.Ordinal) ||
            target is "." or ".." ||
            target.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException(
                "Manifest patch 'target' property must be a file name without directories.");
        }

        return target;
    }

    private static JsonElement ReadManifestElementFromZip(string zipPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        ZipArchiveEntry[] manifests = archive.Entries
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
