using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching.Fields;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class PatchPlanner
{
    private readonly FieldPatchPlanner _fieldPlanner;
    private readonly ReplacementPlanner _replacementPlanner;
    private readonly CopyAssetPlanner _copyAssetPlanner;

    public PatchPlanner(
        FieldPatchPlanner fieldPlanner,
        ReplacementPlanner replacementPlanner,
        CopyAssetPlanner copyAssetPlanner)
    {
        _fieldPlanner = fieldPlanner;
        _replacementPlanner = replacementPlanner;
        _copyAssetPlanner = copyAssetPlanner;
    }

    public PatchPlanningResult Plan(PatchPlanningRequest request)
    {
        if (request.Targets.Count == 0)
        {
            return Failure(request, PatchDiagnosticCode.InvalidPatchConfiguration,
                "Patch config did not contain a target for the assets file.");
        }

        try
        {
            EnsurePatchTargetsCanBePlanned(request.Targets);

            PatchPlan plan;
            PatchPreviewResult preview;

            if (PatchOperationRules.HasReplacementOperations(request.Targets))
            {
                ReplacementPlanningOutput output = _replacementPlanner.Plan(
                    request.AssetsFilePath, request.Targets, request.SourceAssetsPaths);
                preview = output.Preview;
                plan = new AssetReplacementPlan(output.Replacements);
            }
            else if (PatchOperationRules.HasCopyOperations(request.Targets))
            {
                FieldPatchPlanningOutput fieldOutput = _fieldPlanner.Plan(
                    request.AssetsFilePath,
                    request.Targets.Where(PatchOperationRules.HasFieldPatchOperations).ToArray(),
                    request.IncludePreviewDetails);
                CopyAssetPlanningOutput copyOutput = _copyAssetPlanner.Plan(
                    request.AssetsFilePath,
                    request.Targets);
                preview = new PatchPreviewResult(
                    [.. fieldOutput.Preview.Assets, .. copyOutput.Preview.Assets]);
                plan = new FieldPatchAndCopyPlan(fieldOutput.Assets, copyOutput.Copies);
            }
            else
            {
                FieldPatchPlanningOutput output = _fieldPlanner.Plan(
                    request.AssetsFilePath, request.Targets, request.IncludePreviewDetails);
                preview = output.Preview;
                plan = new FieldPatchPlan(output.Assets);
            }

            if (!HasMatchedAssets(plan))
            {
                return Failure(request, PatchDiagnosticCode.NoMatchingAssets,
                    "Patch config did not match any assets.",
                    preview);
            }

            return new PatchPlanningResult(plan, preview, null);
        }
        catch (PatchPlanningException exception)
        {
            PatchDiagnostic diagnostic = exception.Diagnostic with
            {
                AssetsFilePath = request.AssetsFilePath
            };

            return new PatchPlanningResult(null, new PatchPreviewResult([], diagnostic), diagnostic);
        }
    }

    private static bool HasMatchedAssets(PatchPlan plan)
    {
        return plan switch
        {
            FieldPatchPlan fieldPlan => fieldPlan.Assets.Count > 0,
            AssetReplacementPlan replacementPlan => replacementPlan.Replacements.Count > 0,
            FieldPatchAndCopyPlan copyPlan => copyPlan.Copies.Count > 0,
            _ => throw new ArgumentOutOfRangeException(nameof(plan)),
        };
    }

    private static PatchPlanningResult Failure(
        PatchPlanningRequest request,
        PatchDiagnosticCode code,
        string detail,
        PatchPreviewResult? preview = null)
    {
        var diagnostic = new PatchDiagnostic(code, request.AssetsFilePath, Detail: detail);

        return new PatchPlanningResult(null,
            preview is null
                ? new PatchPreviewResult([], diagnostic)
                : preview with { Diagnostic = diagnostic },
            diagnostic);
    }

    private static void EnsurePatchTargetsCanBePlanned(IReadOnlyList<ModPatch> targets)
    {
        if (!PatchOperationRules.HasPatchOperations(targets))
        {
            throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                "Patch config must contain a non-empty 'set', 'add', 'replaceAsset', or 'copyAsset' operation.");
        }

        if (PatchOperationRules.HasReplacementOperations(targets))
        {
            PatchOperationRules.EnsureReplacementOperationsAreNotMixed(targets);
        }

        PatchOperationRules.EnsureCopyOperationsAreValid(targets);
    }
}
