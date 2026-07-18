namespace UnityAssetsPatcher.Application.Assets;

public static class AssetFieldNavigator
{
    public static AssetField? Find(AssetField fieldTree, string path)
    {
        return AssetFieldPath.Find(
            fieldTree,
            path,
            static field => field.Name,
            static field => field.Children,
            static field => field.Value?.ToInvariantString(),
            static (field, name) => field.FindChildren(name));
    }

    public static AssetField? ResolveArray(AssetField? field)
    {
        if (field is null)
        {
            return null;
        }

        if (IsArray(field))
        {
            return field;
        }

        AssetField? namedArray = field.FindChild("Array");

        return namedArray is not null && IsArray(namedArray)
            ? namedArray
            : field.Children.FirstOrDefault(IsArray);
    }

    public static IReadOnlyList<AssetField> GetArrayElements(AssetField arrayField)
    {
        var dataChildren = arrayField.FindChildren("data");

        return dataChildren.Count > 0
            ? dataChildren
            : arrayField.Children.Where(child => !IsArraySizeMetadata(child)).ToArray();
    }

    private static bool IsArray(AssetField field)
    {
        return string.Equals(field.Name, "Array", StringComparison.Ordinal) ||
               string.Equals(field.TypeName, "Array", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArraySizeMetadata(AssetField field)
    {
        return string.Equals(field.Name, "size", StringComparison.Ordinal) &&
               field.Value is AssetFieldValue.Int64 or AssetFieldValue.UInt64;
    }
}
