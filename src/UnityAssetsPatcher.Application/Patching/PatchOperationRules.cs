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
               target.ReplaceFrom is not null;
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

    public static void EnsureReplacementOperationsAreNotMixed(IReadOnlyList<ManifestPatch> targets)
    {
        if (targets.Any(HasFieldPatchOperations))
        {
            throw new InvalidOperationException(
                "Manifest 'replaceFrom' operations cannot be combined with 'set' or 'add' operations for the same assets file.");
        }
    }
}
