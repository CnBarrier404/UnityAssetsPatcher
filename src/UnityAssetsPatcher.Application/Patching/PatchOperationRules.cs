using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Patching;

public static class PatchOperationRules
{
    public static bool HasPatchOperations(IReadOnlyList<ManifestPatch> targets)
    {
        return targets.Count > 0 && targets.All(HasPatchOperations);
    }

    public static bool HasPatchOperations(ManifestPatch target)
    {
        return HasFieldPatchOperations(target) ||
               target.ReplaceFrom is not null ||
               target.CopyAssetFrom is not null;
    }

    public static bool HasFieldPatchOperations(ManifestPatch target)
    {
        return target.SetOperations is { Count: > 0 } ||
               target.AddOperations is { Count: > 0 };
    }

    public static bool HasReplacementOperations(IReadOnlyList<ManifestPatch> targets)
    {
        return targets.Any(target => target.ReplaceFrom is not null);
    }

    public static bool HasCopyOperations(IReadOnlyList<ManifestPatch> targets)
    {
        return targets.Any(target => target.CopyAssetFrom is not null);
    }

    public static void EnsureReplacementOperationsAreNotMixed(IReadOnlyList<ManifestPatch> targets)
    {
        if (targets.Any(HasFieldPatchOperations) || HasCopyOperations(targets))
        {
            throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                "Manifest 'replaceAsset' operations cannot be combined with 'set', 'add', or 'copyAsset' operations for the same assets file.");
        }
    }

    public static void EnsureCopyOperationsAreValid(IReadOnlyList<ManifestPatch> targets)
    {
        foreach (ManifestPatch target in targets.Where(target => target.CopyAssetFrom is not null))
        {
            if (HasFieldPatchOperations(target) || target.ReplaceFrom is not null ||
                target.ComponentTypeName is not null)
            {
                throw new PatchPlanningException(
                    PatchDiagnosticCode.InvalidPatchConfiguration,
                    "Manifest 'copyAsset' cannot be combined with 'set', 'add', 'replaceAsset', or 'componentType' in the same patch.");
            }
        }
    }

    public static void ValidateModManifest(ModManifest manifest)
    {
        if (HasPatchOperations(manifest.Patches))
        {
            return;
        }

        throw new PatchPlanningException(
            PatchDiagnosticCode.InvalidPatchConfiguration,
            "Patch config must contain a non-empty 'set', 'add', 'replaceAsset', or 'copyAsset' operation.");
    }
}
