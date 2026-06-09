using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class ManifestPatchOperationValidator
{
    public void Execute(ModManifest manifest)
    {
        if (PatchOperationRules.HasPatchOperations(manifest.Patches))
        {
            return;
        }

        throw new InvalidOperationException(
            "Patch config must contain a non-empty 'set', 'add', or 'replaceFrom' operation.");
    }
}
