namespace UnityAssetsPatcher.Core.Assets;

public static class AssetFieldNavigator
{
    public static AssetsFieldInfo? FindField(AssetsFieldInfo fieldTree, string path)
    {
        return AssetFieldPathNavigator.Find(
            fieldTree,
            path,
            static field => field.Name,
            static field => field.Children,
            static field => field.Value,
            static (field, name) => field.ChildrenNamed(name));
    }

    public static AssetsFieldInfo? ResolveArrayField(AssetsFieldInfo? field)
    {
        if (field is null)
        {
            return null;
        }

        if (IsArrayField(field))
        {
            return field;
        }

        AssetsFieldInfo? namedArray = field.Child("Array");

        return namedArray is not null && IsArrayField(namedArray)
            ? namedArray
            : field.Children.FirstOrDefault(IsArrayField);
    }

    public static IReadOnlyList<AssetsFieldInfo> GetArrayElementFields(AssetsFieldInfo arrayField)
    {
        var dataChildren = arrayField.ChildrenNamed("data");

        return dataChildren.Count > 0
            ? dataChildren
            : arrayField.Children.Where(child => !IsArraySizeMetadata(child)).ToArray();
    }

    private static bool IsArrayField(AssetsFieldInfo field)
    {
        return string.Equals(field.Name, "Array", StringComparison.Ordinal) ||
               AssetFieldTypeNames.IsArray(field.TypeName);
    }

    private static bool IsArraySizeMetadata(AssetsFieldInfo field)
    {
        return string.Equals(field.Name, "size", StringComparison.Ordinal) &&
               AssetFieldTypeNames.IsInteger(field.TypeName);
    }
}
