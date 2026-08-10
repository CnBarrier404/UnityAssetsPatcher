using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public static class ModPackageErrorCodes
{
    public static OperationErrorCode InvalidPackage { get; } = new("mod_package.invalid");
    public static OperationErrorCode InvalidArchive { get; } = new("mod_package.invalid_archive");
}
