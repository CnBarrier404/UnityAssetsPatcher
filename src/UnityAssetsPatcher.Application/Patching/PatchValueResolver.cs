using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Json;

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
        return JsonElementFactory.Number(pathId);
    }

    private long ResolvePathIdReference(string assetsFilePath, JsonElement resolver)
    {
        string type = ReadRequiredPathIdResolverString(resolver, "type");
        var includeGroups = ReadPathIdResolverMatchGroups(resolver);

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

    private static IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> ReadPathIdResolverMatchGroups(
        JsonElement resolver)
    {
        if (!resolver.TryGetProperty("match", out JsonElement matchElement) ||
            matchElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Path ID reference must contain a 'match' object.");
        }

        var includeGroup = matchElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);

        return includeGroup.Count == 0
            ? throw new InvalidOperationException("Path ID reference match object cannot be empty.")
            : [includeGroup];
    }
}
