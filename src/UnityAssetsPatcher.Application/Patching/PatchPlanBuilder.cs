using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class PatchPlanBuilder
{
    private readonly FieldPatchPlanBuilder _fieldPatchPlanBuilder;
    private readonly ReplacementPlanBuilder _replacementPlanBuilder;

    public PatchPlanBuilder(
        FieldPatchPlanBuilder fieldPatchPlanBuilder,
        ReplacementPlanBuilder replacementPlanBuilder)
    {
        _fieldPatchPlanBuilder = fieldPatchPlanBuilder;
        _replacementPlanBuilder = replacementPlanBuilder;
    }

    public PatchPreviewResult CreatePreview(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        string configPath)
    {
        if (targets.Count == 0)
        {
            return new PatchPreviewResult([]);
        }

        EnsurePatchTargetsCanBePlanned(targets);

        return PatchOperationRules.HasReplacementOperations(targets)
            ? _replacementPlanBuilder.CreatePreview(assetsFilePath, targets, configPath)
            : _fieldPatchPlanBuilder.CreatePreview(assetsFilePath, targets);
    }

    public PatchFileWritePlan CreateWritePlan(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        string configPath)
    {
        EnsurePatchTargetsCanBePlanned(targets);

        if (PatchOperationRules.HasReplacementOperations(targets))
        {
            return PatchFileWritePlan.ForReplacements(
                _replacementPlanBuilder.CreateWritePlan(assetsFilePath, targets, configPath));
        }

        return PatchFileWritePlan.ForFieldPatch(_fieldPatchPlanBuilder.CreateWritePlan(assetsFilePath, targets));
    }

    public PatchFileWritePlan CreateRequiredWritePlan(
        string assetsFilePath,
        IReadOnlyList<ManifestPatch> targets,
        string configPath)
    {
        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Patch config did not contain a target for assets file: {Path.GetFileName(assetsFilePath)}");
        }

        PatchFileWritePlan plan = CreateWritePlan(assetsFilePath, targets, configPath);

        if (!plan.HasMatchedAssets)
        {
            throw new InvalidOperationException("Patch config did not match any assets.");
        }

        return plan;
    }

    private static void EnsurePatchTargetsCanBePlanned(IReadOnlyList<ManifestPatch> targets)
    {
        if (!PatchOperationRules.HasPatchOperations(targets))
        {
            throw new InvalidOperationException(
                "Patch config must contain a non-empty 'set', 'add', or 'replaceFrom' operation.");
        }

        if (PatchOperationRules.HasReplacementOperations(targets))
        {
            PatchOperationRules.EnsureReplacementOperationsAreNotMixed(targets);
        }
    }
}

public sealed record PatchFileWritePlan(
    PatchFileWritePlanKind Kind,
    IReadOnlyList<AssetFieldPatch> Assets,
    IReadOnlyList<AssetReplacement> Replacements)
{
    public bool HasMatchedAssets => Kind == PatchFileWritePlanKind.Replacement
        ? Replacements.Count > 0
        : Assets.Count > 0;

    public static PatchFileWritePlan ForFieldPatch(IReadOnlyList<AssetFieldPatch> assets)
    {
        return new PatchFileWritePlan(PatchFileWritePlanKind.FieldPatch, assets, []);
    }

    public static PatchFileWritePlan ForReplacements(IReadOnlyList<AssetReplacement> replacements)
    {
        return new PatchFileWritePlan(PatchFileWritePlanKind.Replacement, [], replacements);
    }
}

public enum PatchFileWritePlanKind
{
    FieldPatch,
    Replacement,
}
