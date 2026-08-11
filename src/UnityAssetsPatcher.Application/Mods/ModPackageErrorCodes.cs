using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public static class ModPackageErrorCodes
{
    public static OperationErrorCode InvalidArchive { get; } = new("mod_package.invalid_archive");
    public static OperationErrorCode MissingManifest { get; } = new("mod_package.missing_manifest");
    public static OperationErrorCode MultipleManifests { get; } = new("mod_package.multiple_manifests");
    public static OperationErrorCode UnsafeEntryPath { get; } = new("mod_package.unsafe_entry_path");
    public static OperationErrorCode DuplicateEntry { get; } = new("mod_package.duplicate_entry");
    public static OperationErrorCode PackageTooLarge { get; } = new("mod_package.package_too_large");
    public static OperationErrorCode ManifestTooLarge { get; } = new("mod_package.manifest_too_large");
    public static OperationErrorCode MissingEntry { get; } = new("mod_package.missing_entry");
    public static OperationErrorCode EntrySizeMismatch { get; } = new("mod_package.entry_size_mismatch");
}
