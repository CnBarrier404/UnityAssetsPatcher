namespace UnityAssetsPatcher.Application.Mods;

internal static class ModManifestMapper
{
    public static ModManifest Map(ManifestDocumentDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        ModFile[] files =
        [
            .. (document.CopyFiles ?? [])
            .Select(MapFile)
        ];
        var patches = MapTargets(document.Targets ?? []);
        ModOptionalGroup[] optionalGroups =
        [
            .. (document.Optional ?? [])
            .Select(MapOptionalGroup)
        ];

        return new ModManifest(
            document.Schema,
            document.Name,
            document.Author,
            document.Version,
            document.Description,
            document.Game,
            files,
            patches,
            optionalGroups);
    }

    private static ModFile MapFile(ManifestFileDto file)
    {
        return new ModFile(file.Source);
    }

    private static ModPatch[] MapTargets(IEnumerable<ManifestTargetDto> targets)
    {
        var patches = new List<ModPatch>();

        foreach (ManifestTargetDto target in targets)
        {
            patches.AddRange((target.Patches ?? []).Select(patch => ModPatchMapper.Map(target.File, patch)));
        }

        return [.. patches];
    }

    private static ModOptionalGroup MapOptionalGroup(ManifestOptionalGroupDto group)
    {
        ModFile[] files =
        [
            .. (group.CopyFiles ?? [])
            .Select(MapFile)
        ];
        var patches = MapTargets(group.Targets ?? []);

        return new ModOptionalGroup(group.Name, group.Description, files, patches);
    }
}
