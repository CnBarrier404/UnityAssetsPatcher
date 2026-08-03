using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.IO;

public static class FileErrorCodes
{
    public static OperationErrorCode NotFound { get; } = new("file.not_found");
    public static OperationErrorCode InvalidPath { get; } = new("file.invalid_path");
    public static OperationErrorCode AccessDenied { get; } = new("file.access_denied");
    public static OperationErrorCode ReadFailed { get; } = new("file.read_failed");
}
