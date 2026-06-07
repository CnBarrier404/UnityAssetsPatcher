using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Application.Manifests;

public static class ManifestFieldOperationReader
{
    public static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadMatchGroups(JsonElement patchElement)
    {
        JsonElement matchElement = JsonUtils.ReadRequiredProperty(
            patchElement,
            "match",
            JsonValueKind.Object,
            "Manifest patch");

        var match = ReadFieldValueMap(matchElement, "Manifest patch match object");

        return [match];
    }

    public static ManifestSetOperation[]? ReadSetOperations(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(patchElement, "set", JsonValueKind.Object, out JsonElement setElement))
        {
            return null;
        }

        return setElement.EnumerateObject()
            .Select(property => ReadSetOperation(property.Name, property.Value))
            .ToArray();
    }

    public static ManifestAddOperation[]? ReadAddOperations(JsonElement patchElement)
    {
        if (!JsonUtils.TryReadProperty(patchElement, "add", JsonValueKind.Object, out JsonElement addElement))
        {
            return null;
        }

        return addElement.EnumerateObject()
            .Select(ReadAddOperation)
            .ToArray();
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
