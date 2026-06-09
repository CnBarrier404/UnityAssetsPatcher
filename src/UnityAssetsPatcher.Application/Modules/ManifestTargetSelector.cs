using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class ManifestTargetSelector
{
    public IReadOnlyList<ManifestPatch> ForAssetsFile(ModManifest manifest, string assetsFilePath)
    {
        string assetsFileName = Path.GetFileName(assetsFilePath);

        return manifest.Patches
            .Where(patch => string.Equals(patch.AssetsFileName, assetsFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
