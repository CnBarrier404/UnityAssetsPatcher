using System.Text.Json;

namespace UnityAssetsPatcher.Application.Patching;

internal sealed record FieldPatchOperationPlan(
    string Path,
    string OldValue,
    JsonElement From,
    JsonElement To,
    bool WillChange,
    bool WriteRequired,
    string? WriteFailureMessage,
    PatchWriteValueValidation WriteValueValidation,
    bool ValidateBeforeFailure,
    string? WriteValueValidationPath = null);

internal enum PatchWriteValueValidation
{
    None,
    Scalar,
    Array,
}
