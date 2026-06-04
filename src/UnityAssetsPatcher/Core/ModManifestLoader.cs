using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public static class ModManifestLoader
{
    public static ModManifest Load(string configPath)
    {
        JsonElement manifestElement = ManifestJsonReader.Read(configPath);
        return Parse(manifestElement);
    }

    private static ModManifest Parse(JsonElement manifestElement)
    {
        // name/author/version are required metadata for every supported manifest shape.
        string name = ReadRequiredMetadataString(manifestElement, "name");
        string author = ReadRequiredMetadataString(manifestElement, "author");
        string version = ReadRequiredMetadataString(manifestElement, "version");
        string? description = ReadOptionalMetadataString(manifestElement, "description");
        var files = ReadOptionalFiles(manifestElement);

        // A manifest can describe multiple targets with patches, or a single target at the root.
        if (!manifestElement.TryGetProperty("patches", out JsonElement patchesElement))
        {
            return new ModManifest(name, author, version, description, files, [ReadPatchTarget(manifestElement)]);
        }

        if (patchesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest 'patches' property must be an array.");
        }

        var targets = patchesElement.EnumerateArray()
            .Select(ReadPatchTarget)
            .ToArray();

        return targets.Length == 0
            ? throw new InvalidOperationException("Manifest 'patches' array cannot be empty.")
            : new ModManifest(name, author, version, description, files, targets);
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

    private static ManifestPatch ReadPatchTarget(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each patch target must be an object.");
        }

        // target/type/include decide which asset type to match in which assets file.
        string assetsFileName = ReadTargetFileName(element);
        string assetTypeName = ReadAssetTypeName(element);
        var includeGroups = ReadIncludeGroups(element);
        var setOperations = ReadOptionalSetOperations(element);
        var addOperations = ReadOptionalAddOperations(element);
        var replaceFrom = ReadOptionalReplaceFrom(element);

        return new ManifestPatch(assetsFileName, assetTypeName, includeGroups, setOperations, addOperations,
            replaceFrom);
    }

    private static string ReadAssetTypeName(JsonElement element)
    {
        return element.TryGetProperty("type", out JsonElement typeElement) &&
               typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? throw new InvalidOperationException("Manifest patch type cannot be empty.")
            : throw new InvalidOperationException("Manifest patch must contain a string 'type' property.");
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadIncludeGroups(JsonElement element)
    {
        if (!element.TryGetProperty("include", out JsonElement includeElement) ||
            includeElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest patch must contain an 'include' array.");
        }

        var includeGroups = new List<IReadOnlyDictionary<string, JsonElement>>();

        // include entries are OR groups; fields inside each group are interpreted by AssetFieldMatcher.
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

    private static ManifestFile[] ReadOptionalFiles(JsonElement element)
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
            .Select(ReadManifestFile)
            .ToArray();
    }

    private static ManifestFile ReadManifestFile(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each files entry must be an object.");
        }

        if (!element.TryGetProperty("source", out JsonElement sourceElement) ||
            sourceElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Each files entry must contain a string 'source' property.");
        }

        string? source = sourceElement.GetString();

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("Each files entry must contain a non-empty string 'source' property.");
        }

        EnsureValidZipRelativePath(source, "files source");

        return new ManifestFile(source);
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

    private static ManifestSetOperation[]? ReadOptionalSetOperations(JsonElement element)
    {
        // set is optional because find only needs include; patch preview/apply validate it later.
        if (!element.TryGetProperty("set", out JsonElement setElement))
        {
            return null;
        }

        if (setElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Manifest patch 'set' property must be an array.");
        }

        return setElement.EnumerateArray()
            .Select(ReadManifestSetOperation)
            .ToArray();
    }

    private static ManifestSetOperation ReadManifestSetOperation(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each set entry must be an object.");
        }

        // Use field as the manifest field-path key; path is rejected to avoid schema aliases.
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
        return new ManifestSetOperation(field, fromElement.Clone(), toElement.Clone());
    }

    private static ManifestAddOperation[]? ReadOptionalAddOperations(JsonElement element)
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
            .Select(ReadManifestAddOperation)
            .ToArray();
    }

    private static ManifestAddOperation ReadManifestAddOperation(JsonElement element)
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

        return new ManifestAddOperation(field, valueElement.Clone());
    }

    private static ManifestReplaceFrom? ReadOptionalReplaceFrom(JsonElement element)
    {
        if (!element.TryGetProperty("replaceFrom", out JsonElement replaceFromElement))
        {
            return null;
        }

        if (replaceFromElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Manifest patch 'replaceFrom' property must be an object.");
        }

        string assetsFilePath = ReadRequiredReplaceFromString(replaceFromElement, "assets");
        string matchFieldPath = ReadRequiredReplaceFromString(replaceFromElement, "match");

        return new ManifestReplaceFrom(assetsFilePath, matchFieldPath);
    }

    private static string ReadRequiredReplaceFromString(JsonElement replaceFromElement, string propertyName)
    {
        if (!replaceFromElement.TryGetProperty(propertyName, out JsonElement propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"Manifest patch 'replaceFrom' must contain a non-empty string '{propertyName}' property.");
        }

        string? value = propertyElement.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Manifest patch 'replaceFrom' must contain a non-empty string '{propertyName}' property.")
            : value;
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

        // target is resolved by file name under the game directory, so paths are rejected.
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
}
