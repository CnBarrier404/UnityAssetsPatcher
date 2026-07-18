using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Patching;

public sealed record PatchPlanningRequest(
    string AssetsFilePath,
    IReadOnlyList<ManifestPatch> Targets,
    IReadOnlyDictionary<string, string> SourceAssetsPaths);

public sealed record PatchPlanningResult(
    PatchPlan? Plan,
    PatchPreviewResult Preview,
    PatchDiagnostic? Diagnostic)
{
    public bool CanApply => Plan is not null && Diagnostic is null;
}

public abstract record PatchPlan;

public sealed record FieldPatchPlan(IReadOnlyList<AssetFieldPatch> Assets) : PatchPlan;

public sealed record AssetReplacementPlan(IReadOnlyList<AssetReplacement> Replacements) : PatchPlan;

public sealed record PatchDiagnostic(
    PatchDiagnosticCode Code,
    string AssetsFilePath,
    long? PathId = null,
    string? FieldPath = null,
    string? Expected = null,
    string? Actual = null,
    string? Detail = null);

public enum PatchDiagnosticCode
{
    InvalidPatchConfiguration,
    NoMatchingAssets,
    FieldNotFound,
    ValueMismatch,
    UnsupportedValue,
    PathIdReferenceNotFound,
    PathIdReferenceAmbiguous,
    ReplacementSourceNotFound,
    ReplacementMatchInvalid,
}

public sealed class PatchPlanningException : InvalidOperationException
{
    public PatchDiagnostic Diagnostic { get; }

    public PatchPlanningException(PatchDiagnostic diagnostic)
        : base(diagnostic.Code.ToString())
    {
        Diagnostic = diagnostic;
    }
}
