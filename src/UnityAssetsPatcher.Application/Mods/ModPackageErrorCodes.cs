using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public static class ModPackageErrorCodes
{
    public static OperationErrorCode InvalidPackage { get; } = new("mod_package.invalid");
    public static OperationErrorCode InvalidArchive { get; } = new("mod_package.invalid_archive");
    public static OperationErrorCode UnsafeEntryPath { get; } = new("mod_package.unsafe_entry_path");
    public static OperationErrorCode DuplicateEntry { get; } = new("mod_package.duplicate_entry");
    public static OperationErrorCode ManifestMissing { get; } = new("mod_package.manifest_missing");
    public static OperationErrorCode MultipleManifests { get; } = new("mod_package.multiple_manifests");
    public static OperationErrorCode ManifestTooLarge { get; } = new("mod_package.manifest_too_large");
    public static OperationErrorCode EntryNotFound { get; } = new("mod_package.entry_not_found");

    public static OperationErrorCode ExtractionLimitExceeded { get; } =
        new("mod_package.extraction_limit_exceeded");
}
