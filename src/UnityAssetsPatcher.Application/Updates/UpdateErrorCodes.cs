using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Updates;

public static class UpdateErrorCodes
{
    public static OperationErrorCode CheckFailed { get; } = new("update.check_failed");
}
