using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Patching;

public static class PatchTargetSelector
{
    public static IReadOnlyList<ManifestPatch> ForAssetsFile(ModManifest manifest, string assetsFilePath)
    {
        string fileName = Path.GetFileName(assetsFilePath);

        return manifest.Patches
            .Where(patch => string.Equals(patch.AssetsFileName, fileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
