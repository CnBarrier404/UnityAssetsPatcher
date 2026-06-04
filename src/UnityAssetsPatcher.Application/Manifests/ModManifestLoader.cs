using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Manifests;

public static class ModManifestLoader
{
    public static ModManifest Load(string configPath)
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
        var files = manifestElement.TryGetProperty("copyFiles", out _)
            ? ReadOptionalCopyFiles(manifestElement)
            : ReadOptionalLegacyFiles(manifestElement);
        var patches = manifestElement.TryGetProperty("targets", out _)
            ? ReadTargets(manifestElement)
            : ReadLegacyTargets(manifestElement);

        return new ModManifest(name, author, version, description, files, patches);
    }

    private static string ReadRequiredMetadataString(JsonElement manifestElement, string propertyName)
    {
        if (!manifestElement.TryGetProperty(propertyName, out JsonElement propertyElement) ||
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

    private static string? ReadOptionalMetadataString(JsonElement manifestElement, string propertyName)
    {
        if (!manifestElement.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return null;
        }

        return propertyElement.ValueKind != JsonValueKind.String
            ? throw new InvalidOperationException($"Manifest '{propertyName}' property must be a string.")
            : propertyElement.GetString();
    }

    private static ManifestFile[] ReadOptionalCopyFiles(JsonElement element)
    {
        if (!element.TryGetProperty("copyFiles", out JsonElement copyFilesElement))
        {
            return [];
        }

        if (copyFilesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest 'copyFiles' property must be an array.");
        }

        return copyFilesElement.EnumerateArray()
            .Select(jsonElement => ReadManifestFile(jsonElement, "copyFiles"))
            .ToArray();
    }

    private static ManifestFile[] ReadOptionalLegacyFiles(JsonElement element)
    {
        if (!element.TryGetProperty("files", out JsonElement filesElement))
        {
            return [];
        }

        if (filesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest 'files' property must be an array.");
        }

        return filesElement.EnumerateArray()
            .Select(fileElement => ReadManifestFile(fileElement, "files"))
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
        if (!manifestElement.TryGetProperty("targets", out JsonElement targetsElement) ||
            targetsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest must contain a 'targets' array.");
        }

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

        if (!targetElement.TryGetProperty("patches", out JsonElement patchesElement) ||
            patchesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Each targets entry must contain a 'patches' array.");
        }

        var patches = patchesElement.EnumerateArray()
            .Select(patchElement => ReadPatchTarget(assetsFileName, patchElement))
            .ToArray();

        return patches.Length == 0
            ? throw new InvalidOperationException("Each targets entry 'patches' array cannot be empty.")
            : patches;
    }

    private static ManifestPatch[] ReadLegacyTargets(JsonElement manifestElement)
    {
        if (!manifestElement.TryGetProperty("patches", out JsonElement patchesElement))
        {
            return [ReadLegacyPatchTarget(manifestElement)];
        }

        if (patchesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest 'patches' property must be an array.");
        }

        var targets = patchesElement.EnumerateArray()
            .Select(ReadLegacyPatchTarget)
            .ToArray();

        return targets.Length == 0
            ? throw new InvalidOperationException("Manifest 'patches' array cannot be empty.")
            : targets;
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
        var replaceFrom = ReadOptionalReplaceAsset(patchElement);

        return new ManifestPatch(assetsFileName, assetTypeName, includeGroups, setOperations, addOperations,
            replaceFrom);
    }

    private static ManifestPatch ReadLegacyPatchTarget(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each patch target must be an object.");
        }

        string assetsFileName = ReadLegacyTargetFileName(element);
        string assetTypeName = ReadAssetTypeName(element);
        var includeGroups = ReadLegacyIncludeGroups(element);
        var setOperations = ReadLegacyOptionalSetOperations(element);
        var addOperations = ReadLegacyOptionalAddOperations(element);
        var replaceFrom = ReadLegacyOptionalReplaceFrom(element);

        return new ManifestPatch(assetsFileName, assetTypeName, includeGroups, setOperations, addOperations,
            replaceFrom);
    }

    private static string ReadAssetTypeName(JsonElement patchElement)
    {
        return ReadRequiredString(patchElement, "type", "Manifest patch");
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadMatchGroups(JsonElement patchElement)
    {
        if (!patchElement.TryGetProperty("match", out JsonElement matchElement) ||
            matchElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Manifest patch must contain a 'match' object.");
        }

        var match = ReadFieldValueMap(matchElement, "Manifest patch match object");

        return [match];
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadLegacyIncludeGroups(JsonElement element)
    {
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

        return includeGroups.Count == 0
            ? throw new InvalidOperationException("Manifest patch include array cannot be empty.")
            : includeGroups;
    }

    private static ManifestSetOperation[]? ReadOptionalSetOperations(JsonElement patchElement)
    {
        if (!patchElement.TryGetProperty("set", out JsonElement setElement))
        {
            return null;
        }

        if (setElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Manifest patch 'set' property must be an object.");
        }

        return setElement.EnumerateObject()
            .Select(property => ReadManifestSetOperation(property.Name, property.Value))
            .ToArray();
    }

    private static ManifestSetOperation[]? ReadLegacyOptionalSetOperations(JsonElement element)
    {
        if (!element.TryGetProperty("set", out JsonElement setElement))
        {
            return null;
        }

        if (setElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest patch 'set' property must be an array.");
        }

        return setElement.EnumerateArray()
            .Select(ReadLegacyManifestSetOperation)
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

    private static ManifestSetOperation ReadLegacyManifestSetOperation(JsonElement element)
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
        EnsureValidFieldPath(field, "set");

        return new ManifestSetOperation(field, fromElement.Clone(), toElement.Clone());
    }

    private static ManifestAddOperation[]? ReadOptionalAddOperations(JsonElement patchElement)
    {
        if (!patchElement.TryGetProperty("add", out JsonElement addElement))
        {
            return null;
        }

        if (addElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Manifest patch 'add' property must be an object.");
        }

        return addElement.EnumerateObject()
            .Select(ReadManifestAddOperation)
            .ToArray();
    }

    private static ManifestAddOperation[]? ReadLegacyOptionalAddOperations(JsonElement element)
    {
        if (!element.TryGetProperty("add", out JsonElement addElement))
        {
            return null;
        }

        if (addElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest patch 'add' property must be an array.");
        }

        return addElement.EnumerateArray()
            .Select(ReadLegacyManifestAddOperation)
            .ToArray();
    }

    private static ManifestAddOperation ReadManifestAddOperation(JsonProperty property)
    {
        EnsureValidFieldPath(property.Name, "add");

        if (property.Value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Each add field value must be an array.");
        }

        return new ManifestAddOperation(property.Name, property.Value.Clone());
    }

    private static ManifestAddOperation ReadLegacyManifestAddOperation(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each add entry must be an object.");
        }

        if (element.TryGetProperty("path", out _))
        {
            throw new InvalidOperationException(
                "Each add entry must use a string 'field' property; 'path' is not supported.");
        }

        if (!element.TryGetProperty("field", out JsonElement fieldElement) ||
            fieldElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Each add entry must contain a string 'field' property.");
        }

        if (!element.TryGetProperty("value", out JsonElement valueElement))
        {
            throw new InvalidOperationException("Each add entry must contain a 'value' property.");
        }

        if (valueElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Each add entry 'value' property must be an array.");
        }

        string field = fieldElement.GetString() ?? throw new InvalidOperationException("Add field cannot be empty.");
        EnsureValidFieldPath(field, "add");

        return new ManifestAddOperation(field, valueElement.Clone());
    }

    private static ManifestReplaceFrom? ReadOptionalReplaceAsset(JsonElement patchElement)
    {
        if (!patchElement.TryGetProperty("replaceAsset", out JsonElement replaceAssetElement))
        {
            return null;
        }

        if (replaceAssetElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Manifest patch 'replaceAsset' property must be an object.");
        }

        string assetsFilePath = ReadRequiredString(replaceAssetElement, "fromFile", "Manifest patch 'replaceAsset'");
        string matchFieldPath = ReadRequiredString(replaceAssetElement, "matchField", "Manifest patch 'replaceAsset'");

        return new ManifestReplaceFrom(assetsFilePath, matchFieldPath);
    }

    private static ManifestReplaceFrom? ReadLegacyOptionalReplaceFrom(JsonElement element)
    {
        if (!element.TryGetProperty("replaceFrom", out JsonElement replaceFromElement))
        {
            return null;
        }

        if (replaceFromElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Manifest patch 'replaceFrom' property must be an object.");
        }

        string assetsFilePath = ReadRequiredString(replaceFromElement, "assets", "Manifest patch 'replaceFrom'");
        string matchFieldPath = ReadRequiredString(replaceFromElement, "match", "Manifest patch 'replaceFrom'");

        return new ManifestReplaceFrom(assetsFilePath, matchFieldPath);
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
        if (!element.TryGetProperty(propertyName, out JsonElement propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"{ownerDescription} must contain a non-empty string '{propertyName}' property.");
        }

        string? value = propertyElement.GetString();

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

    private static string ReadLegacyTargetFileName(JsonElement element)
    {
        string target = ReadRequiredString(element, "target", "Manifest patch");
        EnsureValidTargetFileName(target, "Manifest patch 'target' property");

        return target;
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
