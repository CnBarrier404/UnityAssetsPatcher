using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal static class AssetReplacementCompatibilityValidator
{
    public static void ValidateMetadataAndAssetCompatibility(
        AssetsFileSession targetSession,
        AssetPathId targetPathId,
        AssetsFileSession sourceSession,
        AssetPathId sourcePathId)
    {
        ArgumentNullException.ThrowIfNull(targetSession);
        ArgumentNullException.ThrowIfNull(sourceSession);

        AssetsFile targetFile = targetSession.AssetsFile;
        AssetsFile sourceFile = sourceSession.AssetsFile;
        AssetFileInfo targetInfo = targetSession.GetAssetInfo(targetPathId);
        AssetFileInfo sourceInfo = sourceSession.GetAssetInfo(sourcePathId);

        ValidateFileCompatibility(targetFile, sourceFile);
        ValidateAssetCompatibility(
            targetFile,
            targetInfo,
            sourceFile,
            sourceInfo,
            $"target Path ID {targetPathId} and source Path ID {sourcePathId}");
    }

    public static void ValidateFields(
        AssetsFileSession targetSession,
        AssetPathId targetPathId,
        AssetTypeValueField targetField,
        AssetsFileSession sourceSession,
        AssetPathId sourcePathId,
        AssetTypeValueField sourceField)
    {
        ArgumentNullException.ThrowIfNull(targetSession);
        ArgumentNullException.ThrowIfNull(targetField);
        ArgumentNullException.ThrowIfNull(sourceSession);
        ArgumentNullException.ThrowIfNull(sourceField);

        ValidateTemplateCompatibility(targetField.TemplateField, sourceField.TemplateField, sourcePathId);
        ValidateReferences(
            targetSession,
            targetPathId,
            sourceSession,
            sourcePathId,
            sourceField,
            sourceField.FieldName);
    }

    private static void ValidateFileCompatibility(AssetsFile targetFile, AssetsFile sourceFile)
    {
        if (sourceFile.Metadata.TargetPlatform != targetFile.Metadata.TargetPlatform)
        {
            throw new InvalidOperationException(
                $"Asset replacement is incompatible: source target platform '{sourceFile.Metadata.TargetPlatform}' " +
                $"does not match target platform '{targetFile.Metadata.TargetPlatform}'.");
        }

        if (sourceFile.Metadata.TypeTreeEnabled != targetFile.Metadata.TypeTreeEnabled)
        {
            throw new InvalidOperationException(
                "Asset replacement is incompatible: source and target assets files do not use the same TypeTree " +
                "mode.");
        }
    }

    private static void ValidateAssetCompatibility(
        AssetsFile targetFile,
        AssetFileInfo targetInfo,
        AssetsFile sourceFile,
        AssetFileInfo sourceInfo,
        string context)
    {
        int targetTypeId = targetInfo.GetTypeId(targetFile);
        int sourceTypeId = sourceInfo.GetTypeId(sourceFile);

        if (sourceTypeId != targetTypeId)
        {
            throw new InvalidOperationException(
                $"Asset replacement is incompatible for {context}: source Type ID '{sourceTypeId}' does not " +
                $"match target Type ID '{targetTypeId}'.");
        }

        ushort targetScriptIndex = targetInfo.GetScriptIndex(targetFile);
        ushort sourceScriptIndex = sourceInfo.GetScriptIndex(sourceFile);

        if (sourceScriptIndex != targetScriptIndex)
        {
            throw new InvalidOperationException(
                $"Asset replacement is incompatible for {context}: source script type index " +
                $"'{sourceScriptIndex}' does not match target script type index '{targetScriptIndex}'.");
        }

        ValidateTypeTreeCompatibility(
            targetFile,
            targetInfo,
            sourceFile,
            sourceInfo,
            context);
    }

    private static void ValidateTypeTreeCompatibility(
        AssetsFile targetFile,
        AssetFileInfo targetInfo,
        AssetsFile sourceFile,
        AssetFileInfo sourceInfo,
        string context)
    {
        TypeTreeType? targetTypeTree = FindTypeTree(targetFile, targetInfo);
        TypeTreeType? sourceTypeTree = FindTypeTree(sourceFile, sourceInfo);

        if (targetTypeTree is null != sourceTypeTree is null)
        {
            throw new InvalidOperationException(
                $"Asset replacement is incompatible for {context}: source and target TypeTree definitions " +
                "are not both available.");
        }

        if (targetTypeTree is null || sourceTypeTree is null)
        {
            return;
        }

        if (sourceTypeTree.IsStrippedType != targetTypeTree.IsStrippedType ||
            sourceTypeTree.ScriptTypeIndex != targetTypeTree.ScriptTypeIndex ||
            sourceTypeTree.TypeBlobIsDefinition != targetTypeTree.TypeBlobIsDefinition ||
            sourceTypeTree.IsRefType != targetTypeTree.IsRefType ||
            !AreHashesEqual(sourceTypeTree.ScriptIdHash, targetTypeTree.ScriptIdHash) ||
            !AreHashesEqual(sourceTypeTree.TypeHash, targetTypeTree.TypeHash) ||
            !AreHashesEqual(sourceTypeTree.ExtTypeHash, targetTypeTree.ExtTypeHash) ||
            !AreDependenciesEqual(sourceTypeTree.TypeDependencies, targetTypeTree.TypeDependencies))
        {
            throw new InvalidOperationException(
                $"Asset replacement is incompatible for {context}: source and target TypeTree definitions " +
                "do not match.");
        }
    }

    private static bool AreHashesEqual(Hash128 left, Hash128 right)
    {
        return left.data is null || right.data is null
            ? left.data is null && right.data is null
            : left.data.SequenceEqual(right.data);
    }

    private static bool AreDependenciesEqual(int[]? left, int[]? right)
    {
        return left is null || right is null
            ? left is null && right is null
            : left.SequenceEqual(right);
    }

    private static TypeTreeType? FindTypeTree(AssetsFile file, AssetFileInfo info)
    {
        return file.Metadata.FindTypeTreeTypeByID(
            info.GetTypeId(file),
            info.GetScriptIndex(file));
    }

    private static void ValidateTemplateCompatibility(
        AssetTypeTemplateField targetTemplate,
        AssetTypeTemplateField sourceTemplate,
        AssetPathId sourcePathId)
    {
        ValidateTemplateCompatibility(targetTemplate, sourceTemplate, sourcePathId.ToString());
    }

    private static void ValidateTemplateCompatibility(
        AssetTypeTemplateField targetTemplate,
        AssetTypeTemplateField sourceTemplate,
        string path)
    {
        if (!string.Equals(sourceTemplate.Name, targetTemplate.Name, StringComparison.Ordinal) ||
            !string.Equals(sourceTemplate.Type, targetTemplate.Type, StringComparison.Ordinal) ||
            sourceTemplate.ValueType != targetTemplate.ValueType ||
            sourceTemplate.IsArray != targetTemplate.IsArray ||
            sourceTemplate.IsAligned != targetTemplate.IsAligned ||
            sourceTemplate.HasValue != targetTemplate.HasValue ||
            sourceTemplate.Version != targetTemplate.Version)
        {
            throw new InvalidOperationException(
                $"Asset replacement is incompatible at serialized field '{path}': source and target field " +
                "definitions do not match.");
        }

        if (sourceTemplate.Children.Count != targetTemplate.Children.Count)
        {
            throw new InvalidOperationException(
                $"Asset replacement is incompatible at serialized field '{path}': source and target field " +
                "definitions have different child layouts.");
        }

        for (int index = 0; index < sourceTemplate.Children.Count; index++)
        {
            AssetTypeTemplateField sourceChild = sourceTemplate.Children[index];
            AssetTypeTemplateField targetChild = targetTemplate.Children[index];
            ValidateTemplateCompatibility(
                targetChild,
                sourceChild,
                $"{path}.{sourceChild.Name}");
        }
    }

    private static void ValidateReferences(
        AssetsFileSession targetSession,
        AssetPathId targetPathId,
        AssetsFileSession sourceSession,
        AssetPathId sourcePathId,
        AssetTypeValueField sourceField,
        string path)
    {
        if (IsPointer(sourceField))
        {
            ValidatePointer(
                targetSession,
                targetPathId,
                sourceSession,
                sourcePathId,
                sourceField,
                path);
        }

        for (int index = 0; index < sourceField.Children.Count; index++)
        {
            AssetTypeValueField child = sourceField.Children[index];
            ValidateReferences(
                targetSession,
                targetPathId,
                sourceSession,
                sourcePathId,
                child,
                $"{path}.{child.FieldName}[{index}]");
        }
    }

    private static bool IsPointer(AssetTypeValueField field)
    {
        return field.TemplateField.Type?.StartsWith("PPtr<", StringComparison.Ordinal) == true;
    }

    private static void ValidatePointer(
        AssetsFileSession targetSession,
        AssetPathId targetPathId,
        AssetsFileSession sourceSession,
        AssetPathId sourcePathId,
        AssetTypeValueField pointer,
        string path)
    {
        AssetTypeValueField? fileIdField = FindChild(pointer, "m_FileID");
        AssetTypeValueField? pathIdField = FindChild(pointer, "m_PathID");

        if (fileIdField?.Value?.ValueType != AssetValueType.Int32 ||
            pathIdField?.Value?.ValueType != AssetValueType.Int64)
        {
            throw new InvalidOperationException(
                $"Asset replacement is incompatible at reference '{path}': the PPtr layout is invalid.");
        }

        int fileId = fileIdField.AsInt;
        long pathId = pathIdField.AsLong;

        if (fileId != 0)
        {
            throw new InvalidOperationException(
                $"Asset replacement is unsafe at reference '{path}': external PPtr file ID '{fileId}' " +
                "requires reference remapping, which is not supported.");
        }

        if (pathId == 0)
        {
            return;
        }

        AssetPathId referencedPathId = new(pathId);

        if (!sourceSession.ContainsAsset(referencedPathId) || !targetSession.ContainsAsset(referencedPathId))
        {
            throw new InvalidOperationException(
                $"Asset replacement is unsafe at reference '{path}': local Path ID '{pathId}' is not present " +
                "in both source and target files.");
        }

        if (pathId == sourcePathId.Value && sourcePathId != targetPathId)
        {
            throw new InvalidOperationException(
                $"Asset replacement is unsafe at reference '{path}': the source asset references itself using " +
                $"Path ID '{sourcePathId}', but the target asset uses Path ID '{targetPathId}'.");
        }

        AssetFileInfo sourceReferencedInfo = sourceSession.GetAssetInfo(referencedPathId);
        AssetFileInfo targetReferencedInfo = targetSession.GetAssetInfo(referencedPathId);
        ValidateAssetCompatibility(
            targetSession.AssetsFile,
            targetReferencedInfo,
            sourceSession.AssetsFile,
            sourceReferencedInfo,
            $"local reference Path ID {pathId} at '{path}'");
    }

    private static AssetTypeValueField? FindChild(AssetTypeValueField field, string name)
    {
        return field.Children.FirstOrDefault(child =>
            string.Equals(child.FieldName, name, StringComparison.Ordinal));
    }
}
