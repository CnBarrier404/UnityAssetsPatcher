using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Domain.Json;

namespace UnityAssetsPatcher.Application.Manifests;

public static class ModManifestParser
{
    public const int CurrentSchemaVersion = 1;

    public static ModManifest Parse(JsonElement manifestElement)
    {
        int schemaVersion = ReadSchemaVersion(manifestElement);
        string name = ReadRequiredMetadataString(manifestElement, "name");
        string author = ReadRequiredMetadataString(manifestElement, "author");
        string version = ReadRequiredMetadataString(manifestElement, "version");
        string? description = ReadOptionalMetadataString(manifestElement, "description");
        string? game = ReadOptionalNonEmptyMetadataString(manifestElement, "game");
        var files = ReadOptionalCopyFiles(manifestElement);
        var patches = ReadTargets(manifestElement);
        var optional = ReadOptionalGroups(manifestElement);

        return new ModManifest(schemaVersion, name, author, version, description, game, files, patches, optional);
    }

    private static int ReadSchemaVersion(JsonElement manifestElement)
    {
        JsonElement versionElement = JsonUtils.ReadRequiredProperty(
            manifestElement,
            "schemaVersion",
            JsonValueKind.Number,
            "Manifest");

        if (!versionElement.TryGetInt32(out int schemaVersion))
        {
            throw new InvalidOperationException("Manifest 'schemaVersion' property must be an integer.");
        }

        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported manifest schema version {schemaVersion}. Supported version: {CurrentSchemaVersion}.");
        }

        return schemaVersion;
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadMatch(JsonElement patchElement)
    {
        JsonElement matchElement = JsonUtils.ReadRequiredProperty(
            patchElement,
            "match",
            JsonValueKind.Object,
            "Manifest patch");

        return ReadFieldValueMap(matchElement, "Manifest patch match object");
    }

    private static ManifestSetOperation[]? ReadSetOperations(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(patchElement, "set", JsonValueKind.Object, out JsonElement setElement))
        {
            return null;
        }

        return setElement.EnumerateObject()
            .Select(property => ReadSetOperation(property.Name, property.Value))
            .ToArray();
    }

    private static ManifestAddOperation[]? ReadAddOperations(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(patchElement, "add", JsonValueKind.Object, out JsonElement addElement))
        {
            return null;
        }

        return addElement.EnumerateObject()
            .Select(ReadAddOperation)
            .ToArray();
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

    private static string? ReadOptionalNonEmptyMetadataString(JsonElement manifestElement, string propertyName)
    {
        string? value = ReadOptionalMetadataString(manifestElement, propertyName);

        return value is null
            ? null
            : string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException(
                    $"Manifest '{propertyName}' property must be a non-empty string when present.")
                : value;
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

    private static ManifestOptionalGroup[] ReadOptionalGroups(JsonElement manifestElement)
    {
        if (!JsonUtils.TryReadProperty(manifestElement, "optional", JsonValueKind.Array,
                out JsonElement optionalElement))
        {
            return [];
        }

        var groups = new List<ManifestOptionalGroup>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ManifestOptionalGroup group in optionalElement.EnumerateArray()
                     .Select(ReadOptionalGroup))
        {
            if (!names.Add(group.Name))
            {
                throw new InvalidOperationException(
                    $"Manifest optional group names must be unique (case-insensitive): '{group.Name}'.");
            }

            groups.Add(group);
        }

        return groups.ToArray();
    }

    private static ManifestOptionalGroup ReadOptionalGroup(JsonElement groupElement)
    {
        if (groupElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each optional entry must be an object.");
        }

        string name = ReadRequiredString(groupElement, "name", "Manifest optional group");
        string? description =
            JsonUtils.ReadOptionalStringProperty(groupElement, "description", "Manifest optional group");
        var patches = ReadOptionalTargets(groupElement);
        var files = ReadOptionalCopyFiles(groupElement);

        return patches.Length == 0 && files.Length == 0
            ? throw new InvalidOperationException(
                $"Manifest optional group '{name}' must contain at least one target patch or copyFiles entry.")
            : new ManifestOptionalGroup(
                name,
                description,
                files,
                patches);
    }

    private static ManifestPatch[] ReadOptionalTargets(JsonElement groupElement)
    {
        if (!JsonUtils.TryReadProperty(groupElement, "targets", JsonValueKind.Array, out JsonElement targetsElement))
        {
            return [];
        }

        var patches = new List<ManifestPatch>();

        foreach (JsonElement targetElement in targetsElement.EnumerateArray())
        {
            patches.AddRange(ReadTargetGroup(targetElement));
        }

        return patches.ToArray();
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
        var match = ReadMatch(patchElement);
        var setOperations = ReadSetOperations(patchElement);
        var addOperations = ReadAddOperations(patchElement);
        ManifestReplaceFrom? replaceFrom = ReadOptionalReplaceAsset(patchElement);
        ManifestCopyAssetFrom? copyAssetFrom = ReadOptionalCopyAsset(patchElement);
        string? componentTypeName = ReadOptionalComponentTypeName(patchElement, assetTypeName, replaceFrom);

        return new ManifestPatch(assetsFileName, assetTypeName, match, setOperations, addOperations,
            replaceFrom, componentTypeName, copyAssetFrom);
    }

    private static string ReadAssetTypeName(JsonElement patchElement)
    {
        return ReadRequiredString(patchElement, "type", "Manifest patch");
    }

    private static ManifestReplaceFrom? ReadOptionalReplaceAsset(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(patchElement, "replaceAsset", JsonValueKind.Object,
                out JsonElement replaceAssetElement))
        {
            return null;
        }

        string assetsFilePath = ReadRequiredString(replaceAssetElement, "fromFile", "Manifest patch 'replaceAsset'");
        string matchFieldPath = ReadRequiredString(replaceAssetElement, "matchField", "Manifest patch 'replaceAsset'");
        EnsureValidZipRelativePath(assetsFilePath, "replaceAsset fromFile");

        return new ManifestReplaceFrom(assetsFilePath, matchFieldPath);
    }

    private static ManifestCopyAssetFrom? ReadOptionalCopyAsset(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(patchElement, "copyAsset", JsonValueKind.Object,
                out JsonElement copyAssetElement))
        {
            return null;
        }

        JsonElement fromElement = JsonUtils.ReadRequiredProperty(
            copyAssetElement,
            "from",
            JsonValueKind.Object,
            "Manifest patch 'copyAsset'");
        string assetTypeName = ReadRequiredString(fromElement, "type", "Manifest patch 'copyAsset.from'");
        JsonElement matchElement = JsonUtils.ReadRequiredProperty(
            fromElement,
            "match",
            JsonValueKind.Object,
            "Manifest patch 'copyAsset.from'");

        return new ManifestCopyAssetFrom(
            assetTypeName,
            ReadFieldValueMap(matchElement, "Manifest patch 'copyAsset.from.match' object"));
    }

    private static string? ReadOptionalComponentTypeName(
        JsonElement patchElement,
        string assetTypeName,
        ManifestReplaceFrom? replaceFrom)
    {
        if (patchElement.TryGetProperty("component", out _))
        {
            throw new InvalidOperationException(
                "Manifest patch property 'component' has been renamed to 'componentType'.");
        }

        if (!JsonUtils.TryReadProperty(
                patchElement,
                "componentType",
                JsonValueKind.String,
                out JsonElement componentElement))
        {
            return null;
        }

        string? componentTypeName = componentElement.GetString();

        if (string.IsNullOrWhiteSpace(componentTypeName))
        {
            throw new InvalidOperationException(
                "Manifest patch 'componentType' property must be a non-empty string.");
        }

        if (!string.Equals(assetTypeName, "GameObject", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Manifest patch 'componentType' property can only be used when 'type' is 'GameObject'.");
        }

        if (replaceFrom is not null)
        {
            throw new InvalidOperationException(
                "Manifest patch 'componentType' property cannot be combined with asset replacement.");
        }

        return componentTypeName;
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

        if (Path.IsPathRooted(path) || normalizedPath.StartsWith('/'))
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

    private static ManifestSetOperation ReadSetOperation(string field, JsonElement element)
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

    private static ManifestAddOperation ReadAddOperation(JsonProperty property)
    {
        EnsureValidFieldPath(property.Name, "add");

        return property.Value.ValueKind != JsonValueKind.Array
            ? throw new InvalidOperationException("Each add field value must be an array.")
            : new ManifestAddOperation(property.Name, property.Value.Clone());
    }

    private static Dictionary<string, JsonElement> ReadFieldValueMap(JsonElement element, string propertyDescription)
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

    private static void EnsureValidFieldPath(string field, string propertyDescription)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new InvalidOperationException($"{propertyDescription} field path cannot be empty.");
        }
    }
}
