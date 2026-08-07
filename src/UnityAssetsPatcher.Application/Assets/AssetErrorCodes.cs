using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Assets;

public static class AssetErrorCodes
{
    public static OperationErrorCode NotFound { get; } = new("asset.not_found");
}
