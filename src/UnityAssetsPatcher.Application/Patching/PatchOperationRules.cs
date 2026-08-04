using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Patching;

public static class PatchOperationRules
{
    public static bool HasPatchOperations(IReadOnlyList<ModPatch> targets)
    {
        return targets.Count > 0 && targets.All(HasPatchOperations);
    }

    public static bool HasPatchOperations(ModPatch target)
    {
        return HasFieldPatchOperations(target) ||
               target.ReplaceAsset is not null ||
               target.CopyAsset is not null;
    }

    public static bool HasFieldPatchOperations(ModPatch target)
    {
        return target.SetOperations is { Count: > 0 } ||
               target.AddOperations is { Count: > 0 };
    }

    public static bool HasReplacementOperations(IReadOnlyList<ModPatch> targets)
    {
        return targets.Any(target => target.ReplaceAsset is not null);
    }

    public static bool HasCopyOperations(IReadOnlyList<ModPatch> targets)
    {
        return targets.Any(target => target.CopyAsset is not null);
    }

    public static void EnsureReplacementOperationsAreNotMixed(IReadOnlyList<ModPatch> targets)
    {
        if (targets.Any(HasFieldPatchOperations) || HasCopyOperations(targets))
        {
            throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                "Manifest 'replaceAsset' operations cannot be combined with 'set', 'add', or 'copyAsset' operations for the same assets file.");
        }
    }

    public static void EnsureCopyOperationsAreValid(IReadOnlyList<ModPatch> targets)
    {
        if (targets.Where(target => target.CopyAsset is not null).Any(target => HasFieldPatchOperations(target) ||
                target.ReplaceAsset is not null ||
                target.ComponentTypeName is not null))
        {
            throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                "Manifest 'copyAsset' cannot be combined with 'set', 'add', 'replaceAsset', or 'componentType' in the same patch.");
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
