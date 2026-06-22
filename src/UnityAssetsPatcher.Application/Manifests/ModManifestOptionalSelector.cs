using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Manifests;

public static class ModManifestOptionalSelector
{
    public static ModManifest SelectOptional(this ModManifest manifest, IReadOnlyList<string> selectedNames)
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

        ManifestFile[] files = manifest.Files
            .Concat(selectedGroups.SelectMany(group => group.Files))
            .ToArray();
        ManifestPatch[] patches = manifest.Patches
            .Concat(selectedGroups.SelectMany(group => group.Patches))
            .ToArray();

        EnsureNoDuplicatePayloadFileNames(files);

        return manifest with { Files = files, Patches = patches, Optional = [] };
    }

    private static void EnsureNoDuplicatePayloadFileNames(IReadOnlyList<ManifestFile> files)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ManifestFile file in files)
        {
            string fileName = GetFileName(file.Source);

            if (!seen.Add(fileName))
            {
                throw new InvalidOperationException(
                    $"Optional content produces a duplicate payload file name: '{fileName}'. " +
                    "All copyFiles entries are copied into the same directory, so file names must be unique.");
            }
        }
    }

    private static string GetFileName(string source)
    {
        string normalized = source.Replace('\\', '/');
        int separatorIndex = normalized.LastIndexOf('/');

        return separatorIndex < 0 ? normalized : normalized[(separatorIndex + 1)..];
    }
}
