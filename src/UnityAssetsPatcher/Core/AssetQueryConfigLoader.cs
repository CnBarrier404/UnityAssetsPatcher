using System.Text.Json;
using UnityAssetsPatcher.Utils;

namespace UnityAssetsPatcher.Core;

public static class AssetQueryConfigLoader
{
    public static AssetQueryConfig Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Query config file not found: {configPath}", configPath);
        }

        JsonElement root = JsonUtils.ReadElementFromFile(configPath);

        string type = root.TryGetProperty("type", out JsonElement typeElement) &&
                      typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? throw new InvalidOperationException("Query config type cannot be empty.")
            : throw new InvalidOperationException("Query config must contain a string 'type' property.");

        if (!root.TryGetProperty("include", out JsonElement includeElement) ||
            includeElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Query config must contain an 'include' array.");
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

        if (!root.TryGetProperty("set", out JsonElement setElement))
        {
            return includeGroups.Count == 0
                ? throw new InvalidOperationException("Query config include array cannot be empty.")
                : new AssetQueryConfig(type, includeGroups, setOperations);
        }

        if (setElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Query config 'set' property must be an array.");
        }

        setOperations = setElement.EnumerateArray()
            .Select(ReadPatchSetOperation)
            .ToArray();

        return includeGroups.Count == 0
            ? throw new InvalidOperationException("Query config include array cannot be empty.")
            : new AssetQueryConfig(type, includeGroups, setOperations);
    }

    private static PatchSetOperation ReadPatchSetOperation(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Each set entry must be an object.");
        }

        if (!element.TryGetProperty("path", out JsonElement pathElement) ||
            pathElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Each set entry must contain a string 'path' property.");
        }

        if (!element.TryGetProperty("from", out JsonElement fromElement))
        {
            throw new InvalidOperationException("Each set entry must contain a 'from' property.");
        }

        if (!element.TryGetProperty("to", out JsonElement toElement))
        {
            throw new InvalidOperationException("Each set entry must contain a 'to' property.");
        }

        string path = pathElement.GetString() ?? throw new InvalidOperationException("Set path cannot be empty.");
        return new PatchSetOperation(path, fromElement.Clone(), toElement.Clone());
    }
}
