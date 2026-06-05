using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Utils;

namespace UnityAssetsPatcher.Application.Manifests;

public sealed class ModManifestLoader : IModManifestLoader
{
    public ModManifest Load(string configPath)
    {
        JsonElement manifestElement = ManifestJsonReader.Read(configPath);
        return Parse(manifestElement);
    }

    private static ModManifest Parse(JsonElement manifestElement)
    {
        string name = ReadRequiredMetadataString(manifestElement, "name");
        string author = ReadRequiredMetadataString(manifestElement, "author");
        string version = ReadRequiredMetadataString(manifestElement, "version");
        string? description = ReadOptionalMetadataString(manifestElement, "description");
        var files = ReadOptionalCopyFiles(manifestElement);
        var patches = ReadTargets(manifestElement);

        return new ModManifest(name, author, version, description, files, patches);
    }

    private static string ReadRequiredMetadataString(JsonElement manifestElement, string propertyName)
    {
        string value = JsonUtils.ReadRequiredStringProperty(manifestElement, propertyName, "Manifest");

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Manifest must contain a non-empty string '{propertyName}' property.")
            : value;
    }

    private static string? ReadOptionalMetadataString(JsonElement manifestElement, string propertyName)
    {
        return JsonUtils.ReadOptionalStringProperty(manifestElement, propertyName, "Manifest");
    }

    private static ManifestFile[] ReadOptionalCopyFiles(JsonElement element)
    {
        if (!JsonUtils.TryReadProperty(element, "copyFiles", JsonValueKind.Array, out JsonElement copyFilesElement))
        {
            return [];
        }

        return copyFilesElement.EnumerateArray()
            .Select(jsonElement => ReadManifestFile(jsonElement, "copyFiles"))
            .ToArray();
    }

    private static ManifestFile ReadManifestFile(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Each {propertyName} entry must be an object.");
        }

        string source = ReadRequiredString(element, "source", $"Each {propertyName} entry");
        EnsureValidZipRelativePath(source, $"{propertyName} source");

        return new ManifestFile(source);
    }

    private static ManifestPatch[] ReadTargets(JsonElement manifestElement)
    {
        JsonElement targetsElement =
            JsonUtils.ReadRequiredProperty(manifestElement, "targets", JsonValueKind.Array, "Manifest");

        var patches = new List<ManifestPatch>();

        foreach (JsonElement targetElement in targetsElement.EnumerateArray())
        {
            patches.AddRange(ReadTargetGroup(targetElement));
        }

        return patches.Count == 0
            ? throw new InvalidOperationException("Manifest 'targets' array cannot be empty.")
            : patches.ToArray();
    }

    private static ManifestPatch[] ReadTargetGroup(JsonElement targetElement)
    {
        if (targetElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each targets entry must be an object.");
        }

        string assetsFileName = ReadTargetFileName(targetElement);
        JsonElement patchesElement = JsonUtils.ReadRequiredProperty(
            targetElement,
            "patches",
            JsonValueKind.Array,
            "Each targets entry");

        var patches = patchesElement.EnumerateArray()
            .Select(patchElement => ReadPatchTarget(assetsFileName, patchElement))
            .ToArray();

        return patches.Length == 0
            ? throw new InvalidOperationException("Each targets entry 'patches' array cannot be empty.")
            : patches;
    }

    private static ManifestPatch ReadPatchTarget(string assetsFileName, JsonElement patchElement)
    {
        if (patchElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each patch entry must be an object.");
        }

        string assetTypeName = ReadAssetTypeName(patchElement);
        var includeGroups = ReadMatchGroups(patchElement);
        var setOperations = ReadOptionalSetOperations(patchElement);
        var addOperations = ReadOptionalAddOperations(patchElement);
        ManifestReplaceFrom? replaceFrom = ReadOptionalReplaceAsset(patchElement);
        string? componentTypeName = ReadOptionalComponentTypeName(patchElement, assetTypeName, replaceFrom);

        return new ManifestPatch(assetsFileName, assetTypeName, includeGroups, setOperations, addOperations,
            replaceFrom, componentTypeName);
    }

    private static string ReadAssetTypeName(JsonElement patchElement)
    {
        return ReadRequiredString(patchElement, "type", "Manifest patch");
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadMatchGroups(JsonElement patchElement)
    {
        JsonElement matchElement = JsonUtils.ReadRequiredProperty(
            patchElement,
            "match",
            JsonValueKind.Object,
            "Manifest patch");

        var match = ReadFieldValueMap(matchElement, "Manifest patch match object");

        return [match];
    }

    private static ManifestSetOperation[]? ReadOptionalSetOperations(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(patchElement, "set", JsonValueKind.Object, out JsonElement setElement))
        {
            return null;
        }

        return setElement.EnumerateObject()
            .Select(property => ReadManifestSetOperation(property.Name, property.Value))
            .ToArray();
    }

    private static ManifestSetOperation ReadManifestSetOperation(string field, JsonElement element)
    {
        EnsureValidFieldPath(field, "set");

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each set field value must be an object.");
        }

        if (!element.TryGetProperty("from", out JsonElement fromElement))
        {
            throw new InvalidOperationException("Each set field value must contain a 'from' property.");
        }

        if (!element.TryGetProperty("to", out JsonElement toElement))
        {
            throw new InvalidOperationException("Each set field value must contain a 'to' property.");
        }

        return new ManifestSetOperation(field, fromElement.Clone(), toElement.Clone());
    }

    private static ManifestAddOperation[]? ReadOptionalAddOperations(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(patchElement, "add", JsonValueKind.Object, out JsonElement addElement))
        {
            return null;
        }

        return addElement.EnumerateObject()
            .Select(ReadManifestAddOperation)
            .ToArray();
    }

    private static ManifestAddOperation ReadManifestAddOperation(JsonProperty property)
    {
        EnsureValidFieldPath(property.Name, "add");

        return property.Value.ValueKind != JsonValueKind.Array
            ? throw new InvalidOperationException("Each add field value must be an array.")
            : new ManifestAddOperation(property.Name, property.Value.Clone());
    }

    private static ManifestReplaceFrom? ReadOptionalReplaceAsset(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(
                patchElement,
                "replaceAsset",
                JsonValueKind.Object,
                out JsonElement replaceAssetElement))
        {
            return null;
        }

        string assetsFilePath = ReadRequiredString(replaceAssetElement, "fromFile", "Manifest patch 'replaceAsset'");
        string matchFieldPath = ReadRequiredString(replaceAssetElement, "matchField", "Manifest patch 'replaceAsset'");

        return new ManifestReplaceFrom(assetsFilePath, matchFieldPath);
    }

    private static string? ReadOptionalComponentTypeName(
        JsonElement patchElement,
        string assetTypeName,
        ManifestReplaceFrom? replaceFrom)
    {
        if (!JsonUtils.TryReadProperty(
                patchElement,
                "component",
                JsonValueKind.String,
                out JsonElement componentElement))
        {
            return null;
        }

        string? componentTypeName = componentElement.GetString();

        if (string.IsNullOrWhiteSpace(componentTypeName))
        {
            throw new InvalidOperationException(
                "Manifest patch 'component' property must be a non-empty string.");
        }

        if (!string.Equals(assetTypeName, "GameObject", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Manifest patch 'component' property can only be used when 'type' is 'GameObject'.");
        }

        if (replaceFrom is not null)
        {
            throw new InvalidOperationException(
                "Manifest patch 'component' property cannot be combined with asset replacement.");
        }

        return componentTypeName;
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadFieldValueMap(
        JsonElement element,
        string propertyDescription)
    {
        var values = element.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);

        if (values.Count == 0)
        {
            throw new InvalidOperationException($"{propertyDescription} cannot be empty.");
        }

        foreach (string field in values.Keys)
        {
            EnsureValidFieldPath(field, propertyDescription);
        }

        return values;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string ownerDescription)
    {
        string value = JsonUtils.ReadRequiredStringProperty(element, propertyName, ownerDescription);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"{ownerDescription} must contain a non-empty string '{propertyName}' property.")
            : value;
    }

    private static string ReadTargetFileName(JsonElement targetElement)
    {
        string file = ReadRequiredString(targetElement, "file", "Manifest target");
        EnsureValidTargetFileName(file, "Manifest target 'file' property");

        return file;
    }

    private static void EnsureValidTargetFileName(string fileName, string propertyDescription)
    {
        if (Path.IsPathRooted(fileName) ||
            fileName.Contains('/', StringComparison.Ordinal) ||
            fileName.Contains('\\', StringComparison.Ordinal) ||
            fileName is "." or ".." ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException(
                $"{propertyDescription} must be a file name without directories.");
        }
    }

    private static void EnsureValidZipRelativePath(string path, string propertyName)
    {
        string normalizedPath = path.Replace('\\', '/');

        if (Path.IsPathRooted(path) || normalizedPath.StartsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Manifest {propertyName} must be a relative zip path.");
        }

        string[] segments = normalizedPath.Split('/');

        if (segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".."))
        {
            throw new InvalidOperationException(
                $"Manifest {propertyName} must not contain empty, '.', or '..' segments.");
        }
    }

    private static void EnsureValidFieldPath(string field, string propertyDescription)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new InvalidOperationException($"{propertyDescription} field path cannot be empty.");
        }
    }
}
