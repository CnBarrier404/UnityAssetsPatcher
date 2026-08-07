using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Manifests;

internal sealed record ModManifestSelection
{
    public ModManifest Manifest { get; }
    public IReadOnlyList<string> AppliedOptionalGroups { get; }

    public ModManifestSelection(ModManifest manifest, IEnumerable<string> appliedOptionalGroups)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(appliedOptionalGroups);

        string[] groups = [.. appliedOptionalGroups];

        if (groups.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Applied optional group names cannot be null or empty.",
                nameof(appliedOptionalGroups));
        }

        Manifest = manifest;
        AppliedOptionalGroups = Array.AsReadOnly(groups);
    }
}

internal static class ModManifestOptionalSelector
{
    public static OperationResult<ModManifestSelection> Select(
        ModManifest manifest,
        IReadOnlyList<string> selectedNames)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(selectedNames);

        if (selectedNames.Count == 0)
        {
            return Success(manifest, manifest.Files, manifest.Patches, []);
        }

        var available = new Dictionary<string, ModOptionalGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (ModOptionalGroup group in manifest.OptionalGroups)
        {
            if (!available.TryAdd(group.Name, group))
            {
                return new OperationFailed<ModManifestSelection>(new OperationError(
                    ManifestErrorCodes.DuplicateOptionalGroup,
                    new Dictionary<string, object?>
                    {
                        ["name"] = group.Name,
                    }));
            }
        }

        var selectedGroups = new List<ModOptionalGroup>();

        foreach (string name in selectedNames)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                !available.TryGetValue(name, out ModOptionalGroup? group))
            {
                return new OperationFailed<ModManifestSelection>(new OperationError(
                    ManifestErrorCodes.UnknownOptionalGroup,
                    new Dictionary<string, object?>
                    {
                        ["name"] = name,
                    }));
            }

            selectedGroups.Add(group);
        }

        ModFile[] files = [.. manifest.Files, .. selectedGroups.SelectMany(group => group.Files)];
        string? duplicatePayload = FindDuplicatePayloadFileName(files);

        if (duplicatePayload is not null)
        {
            return new OperationFailed<ModManifestSelection>(new OperationError(
                ManifestErrorCodes.PayloadConflict,
                new Dictionary<string, object?>
                {
                    ["file_name"] = duplicatePayload,
                }));
        }

        ModPatch[] patches = [.. manifest.Patches, .. selectedGroups.SelectMany(group => group.Patches)];
        var selected = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
        string[] appliedNames =
        [
            .. manifest.OptionalGroups
                .Where(group => selected.Contains(group.Name))
                .Select(group => group.Name)
        ];

        return Success(manifest, files, patches, appliedNames);
    }

    private static OperationSucceeded<ModManifestSelection> Success(
        ModManifest source,
        IEnumerable<ModFile> files,
        IEnumerable<ModPatch> patches,
        IEnumerable<string> appliedNames)
    {
        ModFile[] effectiveFiles = [.. files];
        ModPatch[] effectivePatches = [.. patches];
        var effectiveManifest = new ModManifest(
            source.Schema,
            source.Name,
            source.Author,
            source.Version,
            source.Description,
            source.Game,
            effectiveFiles,
            effectivePatches,
            []);
        var selection = new ModManifestSelection(effectiveManifest, appliedNames);

        return new OperationSucceeded<ModManifestSelection>(selection);
    }

    private static string? FindDuplicatePayloadFileName(IEnumerable<ModFile> files)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return (from file in files
                select file.Source.Replace('\\', '/')
                into normalized
                let separatorIndex = normalized.LastIndexOf('/')
                select separatorIndex < 0 ? normalized : normalized[(separatorIndex + 1)..])
            .FirstOrDefault(fileName => !seen.Add(fileName));
    }
}
