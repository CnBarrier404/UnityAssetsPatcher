using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UnityAssetsPatcher.Tests;

internal static class TestManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void Write(string configPath, string patchJson)
    {
        File.WriteAllText(configPath, CreateJson(patchJson));
    }

    public static void WriteZip(string zipPath, string manifestBody, string entryName = "Mod/manifest.json")
    {
        using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        ZipArchiveEntry entry = archive.CreateEntry(entryName);
        using StreamWriter writer = new(entry.Open());
        writer.Write(CreateJson(manifestBody));
    }

    public static string CreateJson(string patchJson)
    {
        JsonObject fragment = JsonNode.Parse(patchJson)?.AsObject() ??
                              throw new InvalidOperationException("Test manifest fragment must be a JSON object.");
        var manifest = new JsonObject
        {
            ["name"] = "Test Mod",
            ["author"] = "UnityAssetsPatcher.Tests",
            ["version"] = "1.0.0",
        };

        foreach (var property in NormalizeFragment(fragment))
        {
            manifest[property.Key] = property.Value?.DeepClone();
        }

        return manifest.ToJsonString(JsonOptions);
    }

    private static JsonObject NormalizeFragment(JsonObject fragment)
    {
        var manifest = new JsonObject();

        CopyIfPresent(fragment, manifest, "game");

        if (fragment.TryGetPropertyValue("copyFiles", out JsonNode? copyFiles))
        {
            manifest["copyFiles"] = copyFiles?.DeepClone();
        }
        else if (fragment.TryGetPropertyValue("files", out JsonNode? files))
        {
            manifest["copyFiles"] = files?.DeepClone();
        }

        if (fragment.TryGetPropertyValue("targets", out JsonNode? targets))
        {
            manifest["targets"] = targets?.DeepClone();
            return manifest;
        }

        var targetGroups = new Dictionary<string, JsonArray>(StringComparer.OrdinalIgnoreCase);

        if (fragment.TryGetPropertyValue("patches", out JsonNode? patchesNode))
        {
            foreach (JsonNode? patchNode in patchesNode?.AsArray() ?? [])
            {
                AddLegacyPatch(targetGroups, patchNode?.AsObject() ??
                                             throw new InvalidOperationException(
                                                 "Test patch entry must be an object."));
            }
        }
        else
        {
            AddLegacyPatch(targetGroups, fragment);
        }

        manifest["targets"] = new JsonArray(targetGroups
            .Select(group => new JsonObject
            {
                ["file"] = group.Key,
                ["patches"] = group.Value,
            })
            .Cast<JsonNode>()
            .ToArray());

        return manifest;
    }

    private static void AddLegacyPatch(IDictionary<string, JsonArray> targetGroups, JsonObject patch)
    {
        string target = patch["target"]?.GetValue<string>() ??
                        throw new InvalidOperationException("Test patch must contain a target.");

        foreach (JsonObject currentPatch in ConvertLegacyPatch(patch))
        {
            if (!targetGroups.TryGetValue(target, out JsonArray? patches))
            {
                patches = [];
                targetGroups.Add(target, patches);
            }

            patches.Add(currentPatch);
        }
    }

    private static IEnumerable<JsonObject> ConvertLegacyPatch(JsonObject patch)
    {
        var matches = patch.TryGetPropertyValue("include", out JsonNode? includeNode)
            ? includeNode?.AsArray().Select(node => node?.DeepClone()).ToArray()
            : [patch["match"]?.DeepClone() ?? new JsonObject()];

        foreach (JsonNode? match in matches ?? [])
        {
            var current = new JsonObject
            {
                ["type"] = patch["type"]?.DeepClone(),
                ["match"] = match,
            };

            CopyIfPresent(patch, current, "component");

            if (patch.TryGetPropertyValue("set", out JsonNode? setNode))
            {
                current["set"] = ConvertLegacySet(setNode);
            }

            if (patch.TryGetPropertyValue("add", out JsonNode? addNode))
            {
                current["add"] = ConvertLegacyAdd(addNode);
            }

            if (patch.TryGetPropertyValue("replaceFrom", out JsonNode? replaceFromNode))
            {
                JsonObject replaceFrom = replaceFromNode?.AsObject() ??
                                         throw new InvalidOperationException("Test replaceFrom must be an object.");
                current["replaceAsset"] = new JsonObject
                {
                    ["fromFile"] = replaceFrom["assets"]?.DeepClone(),
                    ["matchField"] = replaceFrom["match"]?.DeepClone(),
                };
            }
            else
            {
                CopyIfPresent(patch, current, "replaceAsset");
            }

            yield return current;
        }
    }

    private static JsonNode? ConvertLegacySet(JsonNode? setNode)
    {
        if (setNode is not JsonArray setArray)
        {
            return setNode?.DeepClone();
        }

        var set = new JsonObject();

        foreach (JsonNode? operationNode in setArray)
        {
            JsonObject operation = operationNode?.AsObject() ??
                                   throw new InvalidOperationException("Test set operation must be an object.");
            string field = operation["field"]?.GetValue<string>() ??
                           throw new InvalidOperationException("Test set operation must contain field.");
            set[field] = new JsonObject
            {
                ["from"] = operation["from"]?.DeepClone(),
                ["to"] = operation["to"]?.DeepClone(),
            };
        }

        return set;
    }

    private static JsonNode? ConvertLegacyAdd(JsonNode? addNode)
    {
        if (addNode is not JsonArray addArray)
        {
            return addNode?.DeepClone();
        }

        var add = new JsonObject();

        foreach (JsonNode? operationNode in addArray)
        {
            JsonObject operation = operationNode?.AsObject() ??
                                   throw new InvalidOperationException("Test add operation must be an object.");
            string field = operation["field"]?.GetValue<string>() ??
                           throw new InvalidOperationException("Test add operation must contain field.");
            add[field] = operation["value"]?.DeepClone();
        }

        return add;
    }

    private static void CopyIfPresent(JsonObject source, JsonObject destination, string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out JsonNode? value))
        {
            destination[propertyName] = value?.DeepClone();
        }
    }
}
