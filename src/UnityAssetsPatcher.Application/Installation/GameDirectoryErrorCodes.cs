using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Installation;

public static class GameDirectoryErrorCodes
{
    public static OperationErrorCode Required { get; } = new("game_directory.required");
    public static OperationErrorCode NotFound { get; } = new("game_directory.not_found");
}
