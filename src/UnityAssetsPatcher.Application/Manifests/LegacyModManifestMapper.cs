using UnityAssetsPatcher.Application.Contracts;
using LegacyModManifest = UnityAssetsPatcher.Application.Contracts.ModManifest;

namespace UnityAssetsPatcher.Application.Manifests;

public static class LegacyModManifestMapper
{
    public static LegacyModManifest Map(ModManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new LegacyModManifest(
            1,
            manifest.Name,
            manifest.Author,
            manifest.Version,
            manifest.Description,
            manifest.Game,
            [.. manifest.Files.Select(MapFile)],
            [.. manifest.Patches.Select(MapPatch)],
            [.. manifest.OptionalGroups.Select(MapOptionalGroup)]);
    }

    private static ManifestFile MapFile(ModFile file)
    {
        return new ManifestFile(file.Source);
    }

    private static ManifestOptionalGroup MapOptionalGroup(ModOptionalGroup group)
    {
        return new ManifestOptionalGroup(
            group.Name,
            group.Description,
            [.. group.Files.Select(MapFile)],
            [.. group.Patches.Select(MapPatch)]);
    }

    private static ManifestPatch MapPatch(ModPatch patch)
    {
        return new ManifestPatch(
            patch.AssetsFileName,
            patch.AssetTypeName,
            patch.Match,
            patch.SetOperations.Count == 0
                ? null
                :
                [
                    .. patch.SetOperations.Select(operation => new ManifestSetOperation(
                        operation.FieldPath,
                        operation.From,
                        operation.To))
                ],
            patch.AddOperations.Count == 0
                ? null
                :
                [
                    .. patch.AddOperations.Select(operation => new ManifestAddOperation(
                        operation.FieldPath,
                        operation.Value))
                ],
            patch.ReplaceAsset is null
                ? null
                : new ManifestReplaceFrom(
                    patch.ReplaceAsset.SourceAssetsFile,
                    patch.ReplaceAsset.MatchFieldPath),
            patch.ComponentTypeName,
            patch.CopyAsset is null
                ? null
                : new ManifestCopyAssetFrom(patch.CopyAsset.AssetTypeName, patch.CopyAsset.Match));
    }
}

public static class LegacyModManifestSelectionExtensions
{
    public static LegacyModManifest SelectOptional(
        this LegacyModManifest manifest,
        IReadOnlyList<string> selectedNames)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(selectedNames);

        if (selectedNames.Count == 0)
        {
            return manifest with { Optional = [] };
        }

        var available = manifest.Optional.ToDictionary(group => group.Name, StringComparer.OrdinalIgnoreCase);
        var selectedGroups = new List<ManifestOptionalGroup>();

        foreach (string name in selectedNames)
        {
            if (!available.TryGetValue(name, out ManifestOptionalGroup? group))
            {
                throw new InvalidOperationException($"Unknown optional group: '{name}'.");
            }

            selectedGroups.Add(group);
        }

        ManifestFile[] files =
        [
            .. manifest.Files,
            .. selectedGroups.SelectMany(group => group.Files),
        ];
        ManifestPatch[] patches =
        [
            .. manifest.Patches,
            .. selectedGroups.SelectMany(group => group.Patches),
        ];

        EnsureNoDuplicatePayloadFileNames(files);

        return manifest with { Files = files, Patches = patches, Optional = [] };
    }

    private static void EnsureNoDuplicatePayloadFileNames(IReadOnlyList<ManifestFile> files)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ManifestFile file in files)
        {
            string normalized = file.Source.Replace('\\', '/');
            int separatorIndex = normalized.LastIndexOf('/');
            string fileName = separatorIndex < 0 ? normalized : normalized[(separatorIndex + 1)..];

            if (!seen.Add(fileName))
            {
                throw new InvalidOperationException(
                    $"Optional content produces a duplicate payload file name: '{fileName}'.");
            }
        }
    }
}
