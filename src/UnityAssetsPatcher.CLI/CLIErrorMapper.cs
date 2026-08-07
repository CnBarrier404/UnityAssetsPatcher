using UnityAssetsPatcher.Application.Failures;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.CLI;

internal static class CLIErrorMapper
{
    public static OperationError ToOperationError(ApplicationFailureException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new OperationError(new OperationErrorCode(exception.Code), exception.Parameters);
    }
}
