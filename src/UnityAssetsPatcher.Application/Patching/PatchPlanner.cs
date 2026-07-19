using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching.Fields;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class PatchPlanner
{
    private readonly FieldPatchPlanner _fieldPlanner;
    private readonly ReplacementPlanner _replacementPlanner;
    private readonly CopyAssetPlanner? _copyAssetPlanner;

    public PatchPlanner(FieldPatchPlanner fieldPlanner, ReplacementPlanner replacementPlanner)
    {
        _fieldPlanner = fieldPlanner;
        _replacementPlanner = replacementPlanner;
    }

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
                    request.Targets.Where(PatchOperationRules.HasFieldPatchOperations).ToArray());
                CopyAssetPlanningOutput copyOutput = (_copyAssetPlanner ??
                                                      throw new InvalidOperationException(
                                                          "Copy asset planner is not configured.")).Plan(
                    request.AssetsFilePath,
                    request.Targets);
                preview = new PatchPreviewResult(
                    [.. fieldOutput.Preview.Assets, .. copyOutput.Preview.Assets]);
                plan = new FieldPatchAndCopyPlan(fieldOutput.Assets, copyOutput.Copies);
            }
            else
            {
                FieldPatchPlanningOutput output = _fieldPlanner.Plan(
                    request.AssetsFilePath, request.Targets);
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
        catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException)
        {
            PatchDiagnostic diagnostic = PatchDiagnosticClassifier.Classify(request.AssetsFilePath, exception);

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

    private static void EnsurePatchTargetsCanBePlanned(IReadOnlyList<ManifestPatch> targets)
    {
        if (!PatchOperationRules.HasPatchOperations(targets))
        {
            throw new InvalidOperationException(
                "Patch config must contain a non-empty 'set', 'add', 'replaceAsset', or 'copyAsset' operation.");
        }

        if (PatchOperationRules.HasReplacementOperations(targets))
        {
            PatchOperationRules.EnsureReplacementOperationsAreNotMixed(targets);
        }

        PatchOperationRules.EnsureCopyOperationsAreValid(targets);
    }
}

public static class PatchDiagnosticClassifier
{
    public static PatchDiagnostic Classify(string assetsFilePath, Exception exception)
    {
        string detail = exception.Message;
        PatchDiagnosticCode code = detail switch
        {
            _ when detail.StartsWith("Path ID reference did not match", StringComparison.Ordinal) =>
                PatchDiagnosticCode.PathIdReferenceNotFound,
            _ when detail.StartsWith("Path ID reference matched multiple", StringComparison.Ordinal) =>
                PatchDiagnosticCode.PathIdReferenceAmbiguous,
            _ when detail.Contains("unsupported value type", StringComparison.Ordinal) =>
                PatchDiagnosticCode.UnsupportedValue,
            _ when detail.StartsWith("Field not found", StringComparison.Ordinal) =>
                PatchDiagnosticCode.FieldNotFound,
            _ when detail.Contains("does not match expected", StringComparison.Ordinal) =>
                PatchDiagnosticCode.ValueMismatch,
            _ when exception is FileNotFoundException => PatchDiagnosticCode.ReplacementSourceNotFound,
            _ when detail.StartsWith("Replacement", StringComparison.Ordinal) =>
                PatchDiagnosticCode.ReplacementMatchInvalid,
            _ => PatchDiagnosticCode.InvalidPatchConfiguration,
        };

        return new PatchDiagnostic(code, assetsFilePath, Detail: detail);
    }
}
