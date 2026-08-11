using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public static class ManifestErrorCodes
{
    public static OperationErrorCode InvalidJson { get; } = new("manifest.invalid_json");
    public static OperationErrorCode MissingProperty { get; } = new("manifest.missing_property");
    public static OperationErrorCode InvalidPropertyType { get; } = new("manifest.invalid_property_type");
    public static OperationErrorCode BlankProperty { get; } = new("manifest.blank_property");
    public static OperationErrorCode EmptyCollection { get; } = new("manifest.empty_collection");
    public static OperationErrorCode EmptyObject { get; } = new("manifest.empty_object");
    public static OperationErrorCode InvalidComponentType { get; } = new("manifest.invalid_component_type");
    public static OperationErrorCode ConflictingProperties { get; } = new("manifest.conflicting_properties");
    public static OperationErrorCode UnsupportedSchema { get; } = new("manifest.unsupported_schema");
    public static OperationErrorCode InvalidPath { get; } = new("manifest.invalid_path");
    public static OperationErrorCode DuplicateProperty { get; } = new("manifest.duplicate_property");
    public static OperationErrorCode DuplicateOptionalGroup { get; } = new("manifest.duplicate_optional_group");
    public static OperationErrorCode UnknownOptionalGroup { get; } = new("manifest.unknown_optional_group");
    public static OperationErrorCode PayloadConflict { get; } = new("manifest.payload_conflict");
}
