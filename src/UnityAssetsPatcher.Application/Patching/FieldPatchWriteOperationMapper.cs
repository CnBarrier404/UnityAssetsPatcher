using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Patching;

internal static class FieldPatchWriteOperationMapper
{
    public static void AddTo(
        ICollection<FieldPatchOperation> operations,
        FieldPatchOperationPlan operation)
    {
        if (operation.ValidateBeforeFailure)
        {
            ValidateWriteValue(operation);
        }

        if (operation.WriteFailureMessage is not null)
        {
            throw new InvalidOperationException(operation.WriteFailureMessage);
        }

        if (!operation.ValidateBeforeFailure)
        {
            ValidateWriteValue(operation);
        }

        if (operation.WriteRequired)
        {
            operations.Add(new FieldPatchOperation(operation.Path, operation.To.Clone()));
        }
    }

    private static void ValidateWriteValue(FieldPatchOperationPlan operation)
    {
        switch (operation.WriteValueValidation)
        {
            case PatchWriteValueValidation.None:
                break;
            case PatchWriteValueValidation.Scalar:
                PatchFieldValueFormatter.EnsureSupportedPatchValue(
                    operation.To,
                    operation.WriteValueValidationPath ?? operation.Path);
                break;
            case PatchWriteValueValidation.Array:
                PatchFieldValueFormatter.EnsureSupportedPatchArrayValue(
                    operation.To,
                    operation.WriteValueValidationPath ?? operation.Path);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
