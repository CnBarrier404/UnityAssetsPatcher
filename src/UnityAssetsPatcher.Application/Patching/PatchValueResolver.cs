using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class PatchValueResolver
{
    private readonly AssetQueryService _assetQueryService;

    public PatchValueResolver(AssetQueryService assetQueryService)
    {
        _assetQueryService = assetQueryService;
    }

    public ManifestSetOperation ResolveSetOperation(string assetsFilePath, ManifestSetOperation operation)
    {
        JsonElement resolvedTo = ResolvePatchValue(assetsFilePath, operation.To);

        return new ManifestSetOperation(operation.FieldPath, operation.From.Clone(), resolvedTo);
    }

    private JsonElement ResolvePatchValue(string assetsFilePath, JsonElement value)
    {
        if (!TryGetPathIdResolver(value, out JsonElement resolver))
        {
            return value.Clone();
        }

        long pathId = ResolvePathIdReference(assetsFilePath, resolver);
        return JsonSerializer.SerializeToElement(pathId);
    }

    private long ResolvePathIdReference(string assetsFilePath, JsonElement resolver)
    {
        string type = ReadRequiredPathIdResolverString(resolver, "type");
        var includeGroups = ReadPathIdResolverIncludeGroups(resolver);

        var target = new ManifestPatch(
            Path.GetFileName(assetsFilePath),
            type,
            includeGroups,
            null,
            null);
        var matches = _assetQueryService.FindMatches(assetsFilePath, target)
            .Select(match => match.Asset)
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0].PathId,
            0 => throw new InvalidOperationException(
                $"Path ID reference did not match any assets for type '{type}'."),
            _ => throw new InvalidOperationException(
                $"Path ID reference matched multiple assets for type '{type}'.")
        };
    }

    private static bool TryGetPathIdResolver(JsonElement value, out JsonElement resolver)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.EnumerateObject().Count() == 1 &&
            value.TryGetProperty("$pathId", out resolver) &&
            resolver.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        resolver = default;
        return false;
    }

    private static string ReadRequiredPathIdResolverString(JsonElement resolver, string propertyName)
    {
        if (!resolver.TryGetProperty(propertyName, out JsonElement propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"Path ID reference must contain a non-empty string '{propertyName}' property.");
        }

        string? value = propertyElement.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Path ID reference must contain a non-empty string '{propertyName}' property.")
            : value;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadPathIdResolverIncludeGroups(
        JsonElement resolver)
    {
        if (!resolver.TryGetProperty("include", out JsonElement includeElement) ||
            includeElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Path ID reference must contain an 'include' array.");
        }

        var includeGroups = new List<IReadOnlyDictionary<string, JsonElement>>();

        foreach (JsonElement includeGroupElement in includeElement.EnumerateArray())
        {
            if (includeGroupElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Each Path ID reference include entry must be an object.");
            }

            includeGroups.Add(includeGroupElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal));
        }

        return includeGroups.Count == 0
            ? throw new InvalidOperationException("Path ID reference include array cannot be empty.")
            : includeGroups;
    }
}
