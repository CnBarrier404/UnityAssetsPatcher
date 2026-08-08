using System.Text.Json;

namespace UnityAssetsPatcher.Application.Mods;

internal static class ModPatchMapper
{
    public static ModPatch Map(string assetsFileName, ManifestPatchDto patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFileName);
        ArgumentNullException.ThrowIfNull(patch);

        var match = MapFieldValueMap(patch.Match);
        var setOperations = MapSetOperations(patch.Set);
        var addOperations = MapAddOperations(patch.Add);
        ModReplaceAsset? replaceAsset = MapReplaceAsset(patch.ReplaceAsset);
        ModCopyAsset? copyAsset = MapCopyAsset(patch.CopyAsset);

        return new ModPatch(
            assetsFileName,
            patch.Type,
            match,
            setOperations,
            addOperations,
            replaceAsset,
            patch.ComponentType,
            copyAsset);
    }

    private static Dictionary<string, JsonElement> MapFieldValueMap(JsonElement element)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            values.Add(property.Name, property.Value);
        }

        return values;
    }

    private static ModSetOperation[] MapSetOperations(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return [];
        }

        return
        [
            .. element.EnumerateObject()
                .Select(property => new ModSetOperation(
                    property.Name,
                    property.Value.GetProperty("from"),
                    property.Value.GetProperty("to")))
        ];
    }

    private static ModAddOperation[] MapAddOperations(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return [];
        }

        return
        [
            .. element.EnumerateObject()
                .Select(property => new ModAddOperation(property.Name, property.Value))
        ];
    }

    private static ModReplaceAsset? MapReplaceAsset(ManifestReplaceAssetDto? replaceAsset)
    {
        return replaceAsset is null ? null : new ModReplaceAsset(replaceAsset.FromFile, replaceAsset.MatchField);
    }

    private static ModCopyAsset? MapCopyAsset(ManifestCopyAssetDto? copyAsset)
    {
        if (copyAsset is null)
        {
            return null;
        }

        var match = MapFieldValueMap(copyAsset.From.Match);

        return new ModCopyAsset(copyAsset.From.Type, match);
    }
}
